
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
open BookLibrary.Utils

type EmbeddingOrchestrationService(
    textEmbeddingService: ITextEmbeddingService,
    bookService: IBookService,
    vectorDbService: IVectorDbService,
    userTenantResolverService: IUserTenantResolverService,
    tenantViewerAsync: AggregateViewerAsync2<Tenant>,
    userViewerAsync: AggregateViewerAsync2<User>
) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    new (
        configuration: IConfiguration,
        secretsReader: SecretsReader,
        textEmbeddingService: ITextEmbeddingService,
        bookService: IBookService,
        vectorDbService: IVectorDbService,
        userTenantResolverService: IUserTenantResolverService) =
            let eventStore = PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString())
            let tenantViewer = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
            let userViewer = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
            EmbeddingOrchestrationService(textEmbeddingService, bookService, vectorDbService, userTenantResolverService, tenantViewer, userViewer)

    member this.CreateEmbeddingForBook (context: UserContext, bookId: BookId, ?ct: CancellationToken) =
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! (_, tenant) = tenantViewerAsync (Some ct) tenantId.Value
                let! book = bookService.GetBookAsync(context, bookId, ct)
                let! description =
                    book.Description |> Result.ofOption "Book has no description"

                let! userId =
                    match context with
                    | UserContext.Anonymous -> Error "anonymous user is not allowed to create embedding for book"
                    | UserContext.Authenticated(userId, _) -> Ok userId
                do!
                    match context with
                    | UserContext.Authenticated(_, roles) when (roles |> List.contains Role.Admin) -> Ok ()
                    | _ when tenant.OwnerId = userId ->  Ok ()
                    | _ -> Error "user is not allowed to create embedding for book"
                    
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

                let! results =
                    booksToEmbed |> List.traverseTaskResultM (fun book -> this.CreateEmbeddingForBook (context, book.BookId, ct)) 
                return ()
            }

    interface IEmbeddingOrchestrationService with        
        member this.CreateEmbeddingForBookAsync(context: UserContext, bookId: BookId, ct: CancellationToken option): TaskResult<unit,string> = 
            this.CreateEmbeddingForBook (context, bookId, ?ct = ct)
        member this.CreateEmbeddingsForBooksIfMissingAsync(context: UserContext, bookIds: List<BookId>, ct: CancellationToken option): TaskResult<unit,string> = 
            this.CreateEmbeddingsForBooksIfMissing (context, bookIds, ?ct = ct)


    