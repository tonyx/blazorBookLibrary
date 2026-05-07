
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
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! vectorDbItemsWithBookIds = vectorDbService.ReadAllEmbeddingIdsWithBookIdsAsync ct
                let! results = 
                    vectorDbItemsWithBookIds
                    |> Seq.map (fun (embeddingDataId, bookId) -> 
                        task {
                            let! bookResult = bookService.GetBookAsync (context, bookId, ct)
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
                    let! _ = vectorDbService.RemoveEmbeddingsAsync (unexistingBookReferedBookIds, ct)
                    return ()
                else
                    return ()
            }
    member this.AdjustBookStatesReferringMissingEmbeddingsAsync (context: UserContext, ?ct) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            let embeddingIsSome = BookSearchCriteria(fun b -> b.OptionalEmbedding.IsSome)
            let! booksWithEmbeddings = bookService.GetAllAsync(context, criteria = embeddingIsSome, ct = ct)
            
            let bookIdsEmbeddingIds = booksWithEmbeddings |> List.map (fun b -> b.Id, b.OptionalEmbedding.Value)
            let embeddingIds = bookIdsEmbeddingIds |>> snd
            let! missingEmbeddingIds = vectorDbService.EnquiryForMissingEmbeddingsAsync (embeddingIds, ct)
            
            let missingEmbeddingIdsSet = missingEmbeddingIds |> Set.ofList
            let booksToFix = 
                booksWithEmbeddings 
                |> List.filter (fun b -> missingEmbeddingIdsSet.Contains b.OptionalEmbedding.Value)
                |> List.map (fun b -> b.BookId)
            
            if not booksToFix.IsEmpty then
                let! _ = bookService.ForceBulkRemoveEmbeddingsAsync (context, booksToFix, ct)
                return ()
            else
                return ()
        }
    
    member this.CreateDistributionPointAsync(context: UserContext, distributionPoint: DistributionPoint, ?ct: CancellationToken) = 
        taskResult
            {
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
                let ct = defaultArg ct CancellationToken.None
                let command = DistributionPointCommand.AddReferenceUser userId
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (ct |> Some)
            }

    member this.UnassignUserFromDistributionPointAsync(context: UserContext, id: DistributionPointId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let command = DistributionPointCommand.RemoveReferenceUser userId
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (ct |> Some)
            }

    member this.UpdateDistributionPointInfoAsync(context: UserContext, id: DistributionPointId, info: Info, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let command = DistributionPointCommand.UpdateInfo info
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (ct |> Some)
            }

    member this.RenameDistributionPointAsync(context: UserContext, id: DistributionPointId, name: NonEmptyName, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let command = DistributionPointCommand.Rename name
                return!
                    runAggregateCommandMdAsync<DistributionPoint, DistributionPointEvent, string>
                        id.Value
                        eventStore
                        messageSender
                        (context.ToString())
                        command
                        (ct |> Some)
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
        

        