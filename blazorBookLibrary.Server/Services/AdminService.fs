
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
        bookService: IBookService
    ) =
    new(secretsReader: SecretsReader, configuration: IConfiguration, vectorDbService: IVectorDbService, bookService: IBookService) =
        let connectionString = secretsReader.GetBookLibraryConnectionString()
        let messageSenders = MessageSenders.NoSender
        let eventStore = PgStorage.PgEventStore connectionString
        AdminService (eventStore, messageSenders, vectorDbService, bookService)

    member this.PurgeVectorsReferringDroppedBooksAsync (context: UserContext, ?ct) = 
        taskResult {
                do!
                    (context.IsInRole Role.Admin || context.IsInRole Role.Manager)
                    |> Result.ofBool "Adjusting of book states referring missing embeddings allowed only to admins or managers"
                let! vectorDbItemsWithBookIds = vectorDbService.ReadAllEmbeddingIdsWithBookIdsAsync (context.TenantId, ?ct = ct)
                let! results = 
                    vectorDbItemsWithBookIds
                    |> Seq.map (fun (embeddingDataId, bookId) -> 
                        task {
                            let! bookResult = bookService.GetBookAsync (context, bookId, ?ct = ct)
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
                    let! _ = vectorDbService.RemoveEmbeddingsAsync (unexistingBookReferedBookIds, ?ct = ct)
                    return ()
                else
                    return ()
            }
    member this.AdjustBookStatesReferringMissingEmbeddingsAsync (context: UserContext, ?ct) = 
        taskResult {
            do!
                (context.IsInRole Role.Admin || context.IsInRole Role.Manager)
                |> Result.ofBool "Adjusting of book states referring missing embeddings allowed only to admins or managers"
            let embeddingIsSome = BookSearchCriteria(fun b -> b.OptionalEmbedding.IsSome)
            let! booksWithEmbeddings = bookService.GetAllAsync(context, criteria = embeddingIsSome, ?ct = ct)
            
            let bookIdsEmbeddingIds = booksWithEmbeddings |> List.map (fun b -> b.Id, b.OptionalEmbedding.Value)
            let embeddingIds = bookIdsEmbeddingIds |>> snd
            let! missingEmbeddingIds = vectorDbService.EnquiryForMissingEmbeddingsAsync (embeddingIds, ?ct = ct)
            
            let missingEmbeddingIdsSet = missingEmbeddingIds |> Set.ofList
            let booksToFix = 
                booksWithEmbeddings 
                |> List.filter (fun b -> missingEmbeddingIdsSet.Contains b.OptionalEmbedding.Value)
                |> List.map (fun b -> b.BookId)
            
            if not booksToFix.IsEmpty then
                let! _ = bookService.ForceBulkRemoveEmbeddingsAsync (context, booksToFix, ?ct = ct)
                return ()
            else
                return ()
        }
    
    member this.CreateDistributionPointAsync(context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
        taskResult
            {
                do!
                    context.IsInRole Role.Admin
                    |> Result.ofBool "Creating of distribution point allowed only to admins"
                return!
                    runInitAsync<DistributionPoint, DistributionPointEvent, string>
                    eventStore
                    messageSender
                    distributionPoint
                    ct
            }

    member this.AssignUserToDistributionPointAsync(context: UserContext, id: DistributionPointId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                do!
                    context.IsInRole Role.Admin
                    |> Result.ofBool "Assigning user to distribution point allowed only to admins"
                let command = DistributionPointCommand.AddReferenceUser userId
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        ct
            }

    member this.UnassignUserFromDistributionPointAsync(context: UserContext, id: DistributionPointId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                do!
                    context.IsInRole Role.Admin
                    |> Result.ofBool "Unassigning user from distribution point allowed only to admins"
                let command = DistributionPointCommand.RemoveReferenceUser userId
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        ct
            }

    member this.UpdateDistributionPointInfoAsync(context: UserContext, id: DistributionPointId, info: Info, ?ct: CancellationToken) = 
        taskResult
            {
                do!
                    context.IsInRole Role.Admin
                    |> Result.ofBool "Updating of distribution point allowed only to admins"

                let command = DistributionPointCommand.UpdateInfo info
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        ct
            }

    member this.RenameDistributionPointAsync(context: UserContext, id: DistributionPointId, name: NonEmptyName, ?ct: CancellationToken) = 
        taskResult
            {
                do!
                    context.IsInRole Role.Admin
                    |> Result.ofBool "Renaming of distribution point allowed only to admins"
                let command = DistributionPointCommand.Rename name
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        ct
            }

    member this.PurgeDuplicatedVectorsAsync (context: UserContext, ?ct) =
        taskResult {
            do! (context.IsInRole Role.Admin || context.IsInRole Role.Manager)
                |> Result.ofBool "Only admins or managers can purge duplicated vectors"
            
            let! allPairs = vectorDbService.ReadAllEmbeddingIdsWithBookIdsAsync(context.TenantId, ?ct = ct)
            
            let duplicates = 
                allPairs
                |> Seq.groupBy snd // Group by BookId
                |> Seq.filter (fun (_, group) -> Seq.length group > 1)
                |> Seq.toList
            
            for (bookId, group) in duplicates do
                let! book = bookService.GetBookAsync(context, bookId, ?ct = ct)
                match book.OptionalEmbedding with
                | None ->
                    // Link to the first one and remove others
                    let firstEmbeddingId, _ = Seq.head group
                    let others = Seq.tail group |> Seq.map fst |> Seq.toList
                    let! _ = bookService.EmbedDescriptionAsync(context, bookId, firstEmbeddingId, ?ct = ct)
                    if not others.IsEmpty then
                        let! _ = vectorDbService.RemoveEmbeddingsAsync(others, ?ct = ct)
                        ()
                | Some embeddingId ->
                    // Remove all except the one pointed by OptionalEmbedding
                    let toRemove = 
                        group 
                        |> Seq.map fst 
                        |> Seq.filter (fun id -> id <> embeddingId)
                        |> Seq.toList
                    if not toRemove.IsEmpty then
                        let! _ = vectorDbService.RemoveEmbeddingsAsync(toRemove, ?ct = ct)
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
        member this.UpdateDistributionPointInfoAsync(context, distributionPointId, info, ct) = 
            this.UpdateDistributionPointInfoAsync(context, distributionPointId, info, ?ct = ct)        
        member this.RenameDistributionPointAsync(context, distributionPointId, name, ct) = 
            this.RenameDistributionPointAsync(context, distributionPointId, name, ?ct = ct)
        member this.PurgeDuplicatedVectorsAsync(context, ct) =
            this.PurgeDuplicatedVectorsAsync(context, ?ct = ct)
        

        