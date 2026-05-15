
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open BookLibrary.Shared
open BookLibrary.Utils

type AdminService
    (
        eventStore: IEventStore<string>,
        messageSender: MessageSenders,
        vectorDbService: IVectorDbService,
        bookService: IBookService,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>
    ) =
    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenant = tenantViewerAsync (Some ct) context.TenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    new(secretsReader: SecretsReader, configuration: IConfiguration, vectorDbService: IVectorDbService, bookService: IBookService) =
        AdminService (
            PgStorage.PgEventStore (secretsReader.GetBookLibraryConnectionString()), 
            MessageSenders.NoSender, 
            vectorDbService, 
            bookService, 
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> (PgStorage.PgEventStore (secretsReader.GetBookLibraryConnectionString()))
        )

    member this.PurgeVectorsReferringDroppedBooksAsync (context: UserContext, ?ct) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
                do! checkIsGlobalAdminOrTenantManager context ct
                let! vectorDbItemsWithBookIds = vectorDbService.ReadAllEmbeddingIdsWithBookIdsAsync (context.TenantId, ?ct = Some ct)
                let! results = 
                    vectorDbItemsWithBookIds
                    |> Seq.map (fun (embeddingDataId, bookId) -> 
                        task {
                            let! bookResult = bookService.GetBookAsync (context, bookId, ?ct = Some ct)
                            return (embeddingDataId, bookResult.IsError)
                        }
                    )
                    |> Task.WhenAll
                
                let unexistingBookReferedBookIds =
                    results
                    |> Array.filter snd
                    |> Array.map fst
                    |> Array.toList

                if not unexistingBookReferedBookIds.IsEmpty then
                    let! _ = vectorDbService.RemoveEmbeddingsAsync (unexistingBookReferedBookIds, ?ct = Some ct)
                    return ()
                else
                    return ()
            }
    member this.AdjustBookStatesReferringMissingEmbeddingsAsync (context: UserContext, ?ct) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            do! checkIsGlobalAdminOrTenantManager context ct
            let embeddingIsSome = BookSearchCriteria(fun b -> b.OptionalEmbedding.IsSome)
            let! booksWithEmbeddings = bookService.GetAllAsync(context, criteria = embeddingIsSome, ?ct = Some ct)
            
            let bookIdsEmbeddingIds = booksWithEmbeddings |> List.map (fun b -> b.Id, b.OptionalEmbedding.Value)
            let embeddingIds = bookIdsEmbeddingIds |>> snd
            let! missingEmbeddingIds = vectorDbService.EnquiryForMissingEmbeddingsAsync (embeddingIds, ?ct = Some ct)
            
            let missingEmbeddingIdsSet = missingEmbeddingIds |> Set.ofList
            let booksToFix = 
                booksWithEmbeddings 
                |> List.filter (fun b -> missingEmbeddingIdsSet.Contains b.OptionalEmbedding.Value)
                |> List.map (fun b -> b.BookId)
            
            if not booksToFix.IsEmpty then
                let! _ = bookService.ForceBulkRemoveEmbeddingsAsync (context, booksToFix, ?ct = Some ct)
                return ()
            else
                return ()
        }
    
    member this.CreateDistributionPointAsync(context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                do! checkIsGlobalAdminOrTenantManager context ct
                return!
                    runInitAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSender
                    distributionPoint
                    (Some ct)
                    |> TaskResult.ignore
            }

    member this.AssignUserToDistributionPointAsync(context: UserContext, id: DistributionPointId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                do! checkIsGlobalAdminOrTenantManager context ct
                let command = DistributionPointCommand.AddReferenceUser userId
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (Some ct)
                    |> TaskResult.ignore
            }

    member this.UnassignUserFromDistributionPointAsync(context: UserContext, id: DistributionPointId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                do! checkIsGlobalAdminOrTenantManager context ct
                let command = DistributionPointCommand.RemoveReferenceUser userId
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (Some ct)
                    |> TaskResult.ignore
            }

    member this.UpdateDistributionPointInfoAsync(context: UserContext, id: DistributionPointId, info: Info, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                do! checkIsGlobalAdminOrTenantManager context ct

                let command = DistributionPointCommand.UpdateInfo info
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (Some ct)
                    |> TaskResult.ignore
            }

    member this.RenameDistributionPointAsync(context: UserContext, id: DistributionPointId, name: NonEmptyName, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                do! checkIsGlobalAdminOrTenantManager context ct
                let command = DistributionPointCommand.Rename name
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (Some ct)
                    |> TaskResult.ignore
            }

    member this.PurgeDuplicatedVectorsAsync (context: UserContext, ?ct) =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            do! checkIsGlobalAdminOrTenantManager context ct
            
            let! allPairs = vectorDbService.ReadAllEmbeddingIdsWithBookIdsAsync(context.TenantId, ?ct = Some ct)
            
            let duplicates = 
                allPairs
                |> Seq.groupBy snd // Group by BookId
                |> Seq.filter (fun (_, group) -> Seq.length group > 1)
                |> Seq.toList
            
            for (bookId, group) in duplicates do
                let! book = bookService.GetBookAsync(context, bookId, ?ct = Some ct)
                match book.OptionalEmbedding with
                | None ->
                    // Link to the first one and remove others
                    let firstEmbeddingId, _ = Seq.head group
                    let others = Seq.tail group |> Seq.map fst |> Seq.toList
                    let! _ = bookService.EmbedDescriptionAsync(context, bookId, firstEmbeddingId, ?ct = Some ct)
                    if not others.IsEmpty then
                        let! _ = vectorDbService.RemoveEmbeddingsAsync(others, ?ct = Some ct)
                        ()
                | Some embeddingId ->
                    // Remove all except the one pointed by OptionalEmbedding
                    let toRemove = 
                        group 
                        |> Seq.map fst 
                        |> Seq.filter (fun id -> id <> embeddingId)
                        |> Seq.toList
                    if not toRemove.IsEmpty then
                        let! _ = vectorDbService.RemoveEmbeddingsAsync(toRemove, ?ct = Some ct)
                        ()
            
            return ()
        }

    interface IAdminServices with
        member this.PurgeVectorsReferringDroppedBooksAsync (context, ?ct) = 
            this.PurgeVectorsReferringDroppedBooksAsync (context, ?ct = ct)
        member this.AdjustBookStatesReferringMissingEmbeddingsAsync (context, ?ct) = 
            this.AdjustBookStatesReferringMissingEmbeddingsAsync (context, ?ct = ct)
        member this.AssignUserToDistributionPointAsync(context, distributionPointId, userId, ?ct) = 
            this.AssignUserToDistributionPointAsync(context, distributionPointId, userId, ?ct = ct)
        member this.UnassignUserFromDistributionPointAsync(context, distributionPointId, userId, ?ct) = 
            this.UnassignUserFromDistributionPointAsync(context, distributionPointId, userId, ?ct = ct)        
        member this.UpdateDistributionPointInfoAsync(context, distributionPointId, info, ?ct) = 
            this.UpdateDistributionPointInfoAsync(context, distributionPointId, info, ?ct = ct)        
        member this.RenameDistributionPointAsync(context, distributionPointId, name, ?ct) = 
            this.RenameDistributionPointAsync(context, distributionPointId, name, ?ct = ct)
        member this.PurgeDuplicatedVectorsAsync(context, ?ct) =
            this.PurgeDuplicatedVectorsAsync(context, ?ct = ct)
        

        