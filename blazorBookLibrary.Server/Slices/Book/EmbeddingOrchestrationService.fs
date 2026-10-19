
namespace BookLibrary.Services

open System.Threading
open Microsoft.Extensions.Configuration
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open blazorBookLibrary.Shared.Infrastructure.Services
open blazorBookLibrary.Shared.Resources
open Microsoft.Extensions.Localization
open System.Globalization
open System.Runtime.InteropServices
open Microsoft.Extensions.DependencyInjection
open BookLibrary.Utils

type EmbeddingOrchestrationService(
    textEmbeddingService: ITextEmbeddingService,
    bookService: IBookService,
    vectorDbService: IVectorDbService,
    userTenantResolverService: IUserTenantResolverService,
    tenantViewerAsync: AggregateViewerAsync2<Tenant>,
    userViewerAsync: AggregateViewerAsync2<User>,
    [<Optional; DefaultParameterValue(null)>] ?notificationService: INotificationService
) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    [<ActivatorUtilitiesConstructor>]
    new (
        configuration: IConfiguration,
        secretsReader: SecretsReader,
        textEmbeddingService: ITextEmbeddingService,
        bookService: IBookService,
        vectorDbService: IVectorDbService,
        userTenantResolverService: IUserTenantResolverService,
        [<Optional; DefaultParameterValue(null)>] ?notificationService: INotificationService) =
            let eventStore = PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString())
            let tenantViewer = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
            let userViewer = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
            EmbeddingOrchestrationService(textEmbeddingService, bookService, vectorDbService, userTenantResolverService, tenantViewer, userViewer, ?notificationService = notificationService)

    member this.CreateEmbeddingForBook (context: UserContext, bookId: BookId, ?ct: CancellationToken) =
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! (_, tenant) = tenantViewerAsync (Some ct) tenantId.Value
                let! book = bookService.GetBookAsync(context, bookId, ct)
                let! description =
                    book.Description |> Result.ofOption "Book has no description"

                do! Security.checkIsGlobalAdminOrTenantManager tenant context
                    
                let! embedding = textEmbeddingService.GetEmbeddingAsync(context, description, ct)
                let embeddingId = EmbeddingDataId.New()
                let! storeResult = vectorDbService.StoreEmbeddingAsync (embeddingId, tenantId, bookId, embedding, ct)
                let! updateBook = bookService.EmbedDescriptionAsync(context, bookId, embeddingId, ct)
                return ()
            }

    member this.CreateEmbeddingsForBooksIfMissing (context: UserContext, bookIds: List<BookId>, ?ct: CancellationToken) =
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! (_, tenant) = tenantViewerAsync (Some ct) tenantId.Value
                let! books = bookService.GetBooksAsync(context, bookIds, ct)
                let booksToEmbed = 
                    books
                    |> List.filter (fun book -> book.Description.IsSome && not book.HasEmbedding)

                let notifServiceOpt =
                    notificationService
                    |> Option.bind (fun s -> if System.Object.ReferenceEquals(s, null) then None else Some s)

                let mutable successCount = 0
                let mutable halted = false
                let mutable haltedReason = ""

                for book in booksToEmbed do
                    if not halted then
                        if successCount > 0 then
                            do! Task.Delay(250, ct)
                        let! res =
                            this.CreateEmbeddingForBook(context, book.BookId, ct)
                            |> Task.map (function
                                | Ok () -> Ok true
                                | Error e when e.Contains("429") || e.Contains("quota") || e.Contains("limit") || e.Contains("503") ->
                                    halted <- true
                                    haltedReason <- e
                                    Ok false
                                | Error e ->
                                    Ok false)
                        if res then
                            successCount <- successCount + 1

                match notifServiceOpt, context.UserId with
                | Some notifService, Some userId when booksToEmbed.Length > 0 ->
                    let title =
                        if halted then "Bulk Embeddings Paused (Rate Limit)"
                        else "Bulk Embeddings Completed"
                    let content =
                        if halted then
                            $"Generated {successCount} of {booksToEmbed.Length} embeddings. The process was paused because the AI rate limit was reached."
                        else
                            $"Successfully generated {successCount} of {booksToEmbed.Length} embeddings."
                    let notif = Notification.New(userId, title, content, "/books-manager")
                    let! _ = notifService.CreateNotificationAsync(context, notif, ct = ct)
                    ()
                | _ -> ()

                if halted then
                    return! Error $"Embedding generation paused after {successCount} items due to rate limit: {haltedReason}"
                else
                    return ()
            }

    member this.CreateEmbeddingForPaper (context: UserContext, bookId: BookId, paperId: PaperId, text: string, ?storeDescription: bool, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! (_, tenant) = tenantViewerAsync (Some ct) tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            let! book = bookService.GetBookAsync(context, bookId, ct)
            let! paper =
                book.Papers
                |> List.tryFind (fun (p: Paper) -> p.PaperId = paperId)
                |> Result.ofOption "Paper not found"

            match paper.OptionalEmbedding with
            | Some existingEmbId ->
                let! _ =
                    vectorDbService.RemoveEmbeddingAsync(existingEmbId, ct)
                    |> Task.map (function Ok () -> Ok () | Error _ -> Ok ())
                ()
            | None -> ()

            let! embedding = textEmbeddingService.GetEmbeddingAsync(context, text, ct)
            let embeddingId = EmbeddingDataId.New()
            let! _ = vectorDbService.StorePaperEmbeddingAsync(embeddingId, tenantId, bookId, paperId, embedding, ct)

            let descToStore =
                if defaultArg storeDescription true then
                    Some text
                else
                    paper.Description

            let! _ = bookService.SetPaperDescriptionAndEmbeddingAsync(context, bookId, paperId, descToStore, Some embeddingId, ct)
            let! _ = vectorDbService.LinkIntraBookEmbeddingsAsync(tenantId, bookId, ct)
            return ()
        }

    member this.RemoveEmbeddingForPaper (context: UserContext, bookId: BookId, paperId: PaperId, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! (_, tenant) = tenantViewerAsync (Some ct) tenantId.Value
            do! Security.checkIsGlobalAdminOrTenantManager tenant context

            let! book = bookService.GetBookAsync(context, bookId, ct)
            let! paper =
                book.Papers
                |> List.tryFind (fun (p: Paper) -> p.PaperId = paperId)
                |> Result.ofOption "Paper not found"


            match paper.OptionalEmbedding with
            | Some embId ->
                let! _ =
                    vectorDbService.RemoveEmbeddingAsync(embId, ct)
                    |> Task.map (function Ok () -> Ok () | Error _ -> Ok ())
                let! _ = bookService.SetPaperDescriptionAndEmbeddingAsync(context, bookId, paperId, paper.Description, None, ct)
                let! _ = vectorDbService.LinkIntraBookEmbeddingsAsync(tenantId, bookId, ct)
                return ()
            | None ->
                return ()
        }

    interface IEmbeddingOrchestrationService with        
        member this.CreateEmbeddingForBookAsync(context: UserContext, bookId: BookId, ct: CancellationToken option): TaskResult<unit,string> = 
            this.CreateEmbeddingForBook (context, bookId, ?ct = ct)
        member this.CreateEmbeddingsForBooksIfMissingAsync(context: UserContext, bookIds: List<BookId>, ct: CancellationToken option): TaskResult<unit,string> = 
            this.CreateEmbeddingsForBooksIfMissing (context, bookIds, ?ct = ct)
        member this.CreateEmbeddingForPaperAsync(context: UserContext, bookId: BookId, paperId: PaperId, text: string, storeDescription: bool option, ct: CancellationToken option): TaskResult<unit,string> = 
            this.CreateEmbeddingForPaper (context, bookId, paperId, text, ?storeDescription = storeDescription, ?ct = ct)
        member this.RemoveEmbeddingForPaperAsync(context: UserContext, bookId: BookId, paperId: PaperId, ct: CancellationToken option): TaskResult<unit,string> = 
            this.RemoveEmbeddingForPaper (context, bookId, paperId, ?ct = ct)



    