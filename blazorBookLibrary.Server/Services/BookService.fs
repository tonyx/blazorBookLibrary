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
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open BookLibrary.Shared
open BookLibrary.Utils

type BookService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        userViewerAsync: AggregateViewerAsync2<User>,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        distributionPointViewerAsync: AggregateViewerAsync2<DistributionPoint>,
        userTenantResolverService: IUserTenantResolverService,
        textEmbeddingService: ITextEmbeddingService,
        vectorDbService: IVectorDbService
    ) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    let checkIsGlobalAdminOrTenantManagerOrPublicTenant (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrPublicTenant tenant context
        }

    new (eventStore: IEventStore<string>, userTenantResolverService: IUserTenantResolverService, textEmbeddingService: ITextEmbeddingService, vectorDbService: IVectorDbService) =
        BookService (
            eventStore,
            MessageSenders.NoSender,
            getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<DistributionPoint, DistributionPointEvent, string> eventStore,
            userTenantResolverService,
            textEmbeddingService,
            vectorDbService
        )
    new (secretsReader: SecretsReader, userTenantResolverService: IUserTenantResolverService, textEmbeddingService: ITextEmbeddingService, vectorDbService: IVectorDbService) =
        BookService (
            PgStorage.PgEventStore (secretsReader.GetBookLibraryConnectionString ()),
            userTenantResolverService,
            textEmbeddingService,
            vectorDbService
        )

    member this.AddBookAsync (context: UserContext, book: Book, ?ct: CancellationToken) =
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let! authors: List<Author> = 
                    book.Authors
                    |> List.traverseTaskResultM 
                        (fun authorId -> authorViewerAsync (Some ct) authorId.Value  |> TaskResult.map snd )

                do! 
                    (authors |> List.forall (fun author -> tenantId = author.TenantId))
                    |> Result.ofBool "Author tenant id not matching"

                let! result =
                    runInitAsync<Book, BookEvent, string>
                        eventStore
                        messageSenders
                        book
                        (Some ct)

                authors
                |> List.iter (fun author ->
                    let authorKey = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> author.Id
                    DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [| book.BookId.Value |] authorKey
                )

                return result
            }

    member this.AddBooksAsync (context: UserContext, books: List<Book>, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                do! 
                    (books |> List.forall (fun book -> tenantId = book.TenantId))
                    |> Result.ofBool "Book tenant id not matching"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let! result =
                    books
                    |> List.traverseTaskResultM (fun book -> this.AddBookAsync(context, book, ct))
                return () 
            }

    member this.RemoveBookAsync (context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! (v, book) = 
                    bookViewerAsync (Some ct) bookId.Value 

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                // 3-Phase removal for consistency
                match book.OptionalEmbedding with
                | Some embeddingId ->
                    // Phase 1: Remove from Vector database
                    let! _ = vectorDbService.RemoveEmbeddingAsync(embeddingId, ct)
                    // Phase 2: Remove association from Book (event)
                    let! _ = this.RemoveEmbeddingAsync(context, bookId, ct)
                    ()
                | None -> ()

                // Phase 3: Remove the Book itself
                let! result = 
                    runDeleteAsync<Book, BookEvent, string>
                        eventStore
                        messageSenders
                        bookId.Value
                        (fun Book -> Book.CurrentLoan.IsNone && Book.NoReservations)
                        (Some ct)
                return result
            }

    member this.AddAuthorToBookAsync (context: UserContext, authorId: AuthorId, bookId: BookId, dateTime: System.DateTime, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let! author =
                    authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd

                do! 
                    tenantId = author.TenantId
                    |> Result.ofBool "Author tenant id not matching"

                let bookAddAuthorCommand = 
                    BookCommand.AddAuthor (authorId, dateTime)

                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        bookAddAuthorCommand
                        (Some ct)

                let authorKey = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> authorId.Value
                DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [| bookId.Value |] authorKey

                return result
            }

    member this.UpdateTitleAsync (context: UserContext, title: Title, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let dateTime = System.DateTime.UtcNow
                let bookUpdateTitleCommand = 
                    BookCommand.UpdateTitle (title, dateTime)
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookUpdateTitleCommand
                        (Some ct)
                return result
            }

    member this.UpdateDescriptionAsync (context: UserContext, description: string, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct
                    
                let dateTime = System.DateTime.UtcNow
                let bookUpdateDescriptionCommand = 
                    BookCommand.UpdateDescription (description, dateTime)
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookUpdateDescriptionCommand
                        (Some ct)
                return result
            }

    member this.RemoveDescriptionAsync (context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let dateTime = System.DateTime.UtcNow
                let bookRemoveDescriptionCommand = 
                    BookCommand.RemoveDescription dateTime
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookRemoveDescriptionCommand
                        (Some ct)
                return result
            }

    member this.EmbedDescriptionAsync (context: UserContext, bookId: BookId, embeddingId: EmbeddingDataId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                let dateTime = System.DateTime.UtcNow
                let bookEmbedDescriptionCommand = 
                    BookCommand.EmbedDescription (embeddingId, dateTime)
                
                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookEmbedDescriptionCommand
                        (Some ct)
                return result
            }

    member this.RemoveEmbeddingAsync (context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let dateTime = System.DateTime.UtcNow
                let bookRemoveEmbeddingCommand = 
                    BookCommand.RemoveEmbedding dateTime
                
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookRemoveEmbeddingCommand
                        (Some ct)
                return result
            }
    member this.ForceBulkRemoveEmbeddingsAsync (context: UserContext, bookIds: List<BookId>, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! books = 
                    bookIds
                    |> List.traverseTaskResultM (fun bookId -> bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd)

                do! 
                    books |> List.forall (fun book -> tenantId = book.TenantId)
                    |> Result.ofBool "Book tenant id not matching"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                //todo: this will fail if one of them fails. Consider later being more "forcing" on the failures (logging more and keep going)
                let! _ =
                    bookIds
                    |> List.traverseTaskResultM (fun bookId -> 
                        let bookRemoveEmbeddingCommand = 
                            BookCommand.ForceRemoveEmbedding 
                        runAggregateCommandMdAsync<Book, BookEvent, string>
                            bookId.Value
                            eventStore
                            messageSenders
                            ""
                            bookRemoveEmbeddingCommand
                            (Some ct)
                    )
                return ()
            }

    member this.UpdateIsbnAsync (context: UserContext, isbn: Isbn, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                let dateTime = System.DateTime.UtcNow
                let bookUpdateIsbnCommand = 
                    BookCommand.UpdateIsbn (isbn, dateTime)

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"
                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookUpdateIsbnCommand
                        (Some ct)
                return result
            }

    member this.RemoveImageUrlAsync (context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let dateTime = System.DateTime.UtcNow
                let bookRemoveImageUrlCommand = 
                    BookCommand.RemoveImageUrl (dateTime)

                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookRemoveImageUrlCommand
                        (Some ct)
                return result
            }

    member this.SetImageUrlAsync (context: UserContext, bookId: BookId, imageUrl: Uri, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"
                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let dateTime = System.DateTime.UtcNow
                let bookSetImageUrlCommand = 
                    BookCommand.SetImageUrl (imageUrl, dateTime)
                
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        bookSetImageUrlCommand
                        (Some ct)
                return result
            }
    member this.SetAvailabilityAsync (context: UserContext, availability: Availability, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"

                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let dateTime = System.DateTime.UtcNow
                let command = 
                    BookCommand.SetAvailability (availability, dateTime)
                return! 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        book.Id
                        eventStore
                        messageSenders
                        ""
                        command
                        (Some ct)
            }

    member this.BulkEditAsync (context: UserContext, bookIds: List<BookId>, bulkBookEdit: BulkBookEdit, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let! books = 
                    bookIds
                    |> List.traverseTaskResultM (fun bookId -> bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd)
                do! 
                    (books |> List.forall (fun book -> tenantId = book.TenantId))
                    |> Result.ofBool "Book tenant id not matching"
                let! userId = 
                    context.UserId |> Result.ofOption "user must be some for bulkEdit"

                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let dateTime = System.DateTime.UtcNow
                let preExecutedYearEditCommands = 
                    match bulkBookEdit.YearEdit with
                    | Some year -> 
                        let command = BookCommand.UpdateYear (year, dateTime)
                        let! preExecutedYearUpdateCommands =
                            bookIds
                            |> List.map _.Value
                            |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                        match preExecutedYearUpdateCommands with
                        | Error e ->
                            printf "Error pre-executing year update command: %A\n" e
                            None 
                        | Ok v ->
                            Some v
                    | None -> None

                let preExecutedMainCategoryCommands =
                    match bulkBookEdit.MainCategoryEdit with
                    | Some mainCategory -> 
                        let command = BookCommand.ChangeMainCategory (mainCategory, dateTime)
                        let! preExecutedMainCategoryUpdateCommands =
                            bookIds
                            |> List.map _.Value
                            |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                        match preExecutedMainCategoryUpdateCommands with
                        | Error e ->
                            printf "Error pre-executing main category update command: %A\n" e
                            None 
                        | Ok v ->
                            Some v
                    | None -> None

                let preExecutedAdditionalCategory =
                    match bulkBookEdit.AdditionalCategoriesEdit with
                    | Some additionalCategories -> 
                        let command = BookCommand.ReplaceAdditionalCategories (additionalCategories, dateTime)
                        let! preExecutedAdditionalCategoryUpdateCommands =
                            bookIds
                            |> List.map _.Value
                            |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                        match preExecutedAdditionalCategoryUpdateCommands with
                        | Error e ->
                            printf "Error pre-executing additional category update command: %A\n" e
                            None 
                        | Ok v ->
                            Some v
                    | None -> None

                let preExecutedAvailabilityEditCommands =
                    match bulkBookEdit.AvailabilityEdit with
                    | Some availability -> 
                        let command = BookCommand.SetAvailability (availability, dateTime)
                        let! preExecutedAvailabilityUpdateCommands =
                            bookIds
                            |> List.map _.Value
                            |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                        match preExecutedAvailabilityUpdateCommands with
                        | Error e ->
                            printf "Error pre-executing availability update command: %A\n" e
                            None 
                        | Ok v ->
                            Some v
                    | None -> None

                let preExecutedDistributionPointEditCommands =
                    match bulkBookEdit.DistributionPointEdit with
                    | Some distributionPoint -> 
                        let command = BookCommand.SetDistributionPoint (distributionPoint, userId, dateTime)
                        let! preExecutedDistributionPointUpdateCommands =
                            bookIds
                            |> List.map _.Value
                            |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                        match preExecutedDistributionPointUpdateCommands with
                        | Error e ->
                            printf "Error pre-executing distribution point update command: %A\n" e
                            None 
                        | Ok v ->
                            Some v
                    | None -> None

                let preExecutedAuthorEditCommands =
                    match bulkBookEdit.AdditionalAuthorsEdit with
                    | Some authors -> 
                        let command = BookCommand.AddAuthors (authors, dateTime)
                        let! preExecutedAuthorUpdateCommands =
                            bookIds
                            |> List.map _.Value
                            |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                        match preExecutedAuthorUpdateCommands with
                        | Error e ->
                            printf "Error pre-executing author update command: %A\n" e
                            None 
                        | Ok v ->
                            Some v
                    | None -> None

                let allPreExecutedCommands =
                    if preExecutedYearEditCommands.IsSome then
                        preExecutedYearEditCommands.Value
                    else
                        []
                    @
                    if preExecutedMainCategoryCommands.IsSome then
                        preExecutedMainCategoryCommands.Value
                    else
                        []
                    @
                    if preExecutedAdditionalCategory.IsSome then
                        preExecutedAdditionalCategory.Value
                    else
                        []
                    @
                    if preExecutedAvailabilityEditCommands.IsSome then
                        preExecutedAvailabilityEditCommands.Value
                    else
                        []
                    @
                    if preExecutedDistributionPointEditCommands.IsSome then
                        preExecutedDistributionPointEditCommands.Value
                    else
                        []
                    @
                    if preExecutedAuthorEditCommands.IsSome then
                        preExecutedAuthorEditCommands.Value
                    else
                        []
                let result = 
                    runPreExecutedAggregateCommands<string>      
                        allPreExecutedCommands
                        eventStore
                        messageSenders
                return! result
            }

    member this.RemoveAuthorFromBookAsync (context: UserContext, authorId: AuthorId, bookId: BookId, dateTime: System.DateTime, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                let! author = 
                    authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd

                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"
                do!
                    checkIsGlobalAdminOrTenantManager context ct
                
                let bookRemoveAuthorCommand = 
                    BookCommand.RemoveAuthor (authorId, dateTime)
                let! result = 
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        bookRemoveAuthorCommand
                        (Some ct)

                let authorKey = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> authorId.Value
                DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [| bookId.Value |] authorKey

                return result
            }

    member this.GetBookAsync (context: UserContext, id: BookId, ?ct: CancellationToken): Task<Result<Book, string>> = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) id.Value |> TaskResult.map snd
                do! 
                    tenantId = book.TenantId
                    |> Result.ofBool "Book tenant id not matching"
                
                return book
            }

    member this.GetBooksAsync (context: UserContext, bookIds: List<BookId>, ?ct: CancellationToken): Task<Result<List<Book>, string>> =
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! books = 
                    bookIds 
                    |> List.traverseTaskResultM (fun id -> bookViewerAsync (Some ct) id.Value |> TaskResult.map snd)

                do! 
                    (books |> List.forall (fun book -> tenantId = book.TenantId))
                    |> Result.ofBool "Book tenant id not matching"
                
                return books
            }

    member this.GetAllBooksAsync(context: UserContext, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> (fun b -> b.TenantId = tenantId && criteria.Invoke b) eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.GetAllBooksOfTenantAsync(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                // do!
                //     checkIsGlobalAdminOrTenantManager context ct
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> (fun b -> b.TenantId = tenantId) eventStore (Some ct) 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAsync(context: UserContext, title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) && criteria.Invoke book && book.TenantId = tenantId
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByIsbnAsync(context: UserContext, isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase) && criteria.Invoke book && book.TenantId = tenantId
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndIsbnAsync(context: UserContext, title: Title, isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    ((String.IsNullOrWhiteSpace(book.Title.Value) |> not && String.IsNullOrWhiteSpace(title.Value) |> not && book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)) || 
                    (String.IsNullOrWhiteSpace(book.Isbn.Value) |> not && String.IsNullOrWhiteSpace(isbn.Value) |> not && book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)))
                    && criteria.Invoke book && book.TenantId = tenantId

                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByYearAsync(context: UserContext, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    match year with
                    | Before y -> book.Year.Value < y
                    | After y -> book.Year.Value > y
                    | Exact y -> book.Year.Value = y
                    | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2

                let compoundFilter = fun (book: Book) -> 
                    filter book && criteria.Invoke book && book.TenantId = tenantId


                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> compoundFilter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndYearAsync(context: UserContext, title: Title, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    titleMatch && yearMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByIsbnAndYearAsync(context: UserContext, isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    let isbnMatch = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    isbnMatch && yearMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndIsbnAndYearAsync(context: UserContext, title: Title, isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                    let isbnMatch = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    titleMatch && isbnMatch && yearMatch && criteria.Invoke book && tenantId = book.TenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByCategoriesAsync(context: UserContext, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    (categories |> Seq.exists (fun c -> 
                        book.MainCategory = c || (book.AdditionalCategories |> List.contains c)))
                    && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndCategoriesAsync(context: UserContext, title: Title, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                    (categories |> Seq.exists (fun c -> 
                        book.MainCategory = c || (book.AdditionalCategories |> List.contains c)))
                    && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByYearAndCategoriesAsync(context: UserContext, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    let categoryMatch = 
                        categories |> Seq.exists (fun c -> 
                            book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                    yearMatch && categoryMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndYearAndCategoriesAsync(context: UserContext, title: Title, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                do!
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct

                let filter (book: Book) = 
                    let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    let categoryMatch = 
                        categories |> Seq.exists (fun c -> 
                            book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                    titleMatch && yearMatch && categoryMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByIsbnOrTitleAsync(context: UserContext, isbn: Isbn, title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                do!
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct

                let filter (book: Book) = 
                    (book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase) ||
                    book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase))
                    && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
        }

    member this.ChangeMainCategoryAsync(context: UserContext, category: Category, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let command = BookCommand.ChangeMainCategory (category, System.DateTime.Now)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }

    member this.AddAdditionalCategoryAsync(context: UserContext, category: Category, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let command = BookCommand.AddAdditionalCategory (category, System.DateTime.Now)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }

    member this.RemoveAdditionalCategoryAsync(context: UserContext, category: Category, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd

                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let command = BookCommand.RemoveAdditionalCategory (category, System.DateTime.Now)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }
    member this.AddTagToBookAsync(context: UserContext, tag: Tag, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct
                let command = BookCommand.AddTag (tag, System.DateTime.Now)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }
    member this.RemoveTagFromBookAsync(context: UserContext, tag: Tag, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct
                let command = BookCommand.RemoveTag (tag, System.DateTime.Now)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }
    member this.SealAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let command = BookCommand.Seal (System.DateTime.UtcNow)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }
    member this.UnsealAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct
                let command = BookCommand.Unseal (System.DateTime.UtcNow)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }

    member this.SetDistributionPointAsync(context: UserContext, distributionPointId: DistributionPointId, bookId: BookId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None

                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                let! distributionPoint = distributionPointViewerAsync (ct |> Some) distributionPointId.Value |> TaskResult.map snd

                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    distributionPoint.TenantId = tenantId
                    |> Result.ofBool "Distribution point not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let command = BookCommand.SetDistributionPoint(distributionPointId, userId, DateTime.UtcNow)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (ct |> Some)
            }

    member this.UnSetDistributionPointAsync(context: UserContext, distributionPointId: DistributionPointId, bookId: BookId, userId: UserId, ?ct: CancellationToken) = 
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! book = bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                let! distributionPoint = distributionPointViewerAsync (ct |> Some) distributionPointId.Value |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool "Book not found in tenant"
                do!
                    distributionPoint.TenantId = tenantId
                    |> Result.ofBool "Distribution point not found in tenant"
                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let command = BookCommand.UnsetDistributionPoint(userId, DateTime.UtcNow)
                return!
                    runAggregateCommandMdAsync<Book, BookEvent, string>
                        bookId.Value
                        eventStore
                        messageSenders
                        ""
                        command
                        (Some ct)
            }

    member this.UnsetAllBookRelatedToDPAsync (context: UserContext, distributionPointId: DistributionPointId, userId: UserId, ?ct: CancellationToken) =
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let ct = defaultArg ct CancellationToken.None
                let! books = 
                    StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> 
                        (fun book -> book.DistributionPoint = Some distributionPointId)
                        eventStore
                        (ct |> Some)
                            
                let books = books |>> snd
                do!
                    (books |> List.forall (fun book -> book.TenantId = tenantId))
                    |> Result.ofBool "Book not found in tenant"

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let booksIds = books |>> _.Id
                let unsetCommand: List<AggregateCommand<Book, BookEvent>> = 
                    [ 1 .. booksIds.Length ]
                    |>> fun _ -> BookCommand.UnsetDistributionPoint (userId, DateTime.UtcNow)

                let! result =
                    runNAggregateCommandsMdAsync<Book, BookEvent, string>
                        booksIds
                        eventStore
                        messageSenders
                        ""
                        unsetCommand
                        (Some ct)
                return result
            }
            
    member this.MoveFromDpToAnotherDPAsync(context: UserContext, fromPoint: DistributionPointId, toPoint: DistributionPointId, userId: UserId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let! books = 
                    StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string>
                        (fun book -> book.DistributionPoint = Some fromPoint)
                        eventStore
                        (Some ct)
                let books = books |>> snd
                do!
                    (books |> List.forall (fun book -> book.TenantId = tenantId))
                    |> Result.ofBool "Book not found in tenant"        

                do!
                    checkIsGlobalAdminOrTenantManager context ct

                let bookIds = books |>> _.Id

                let! dp1 = distributionPointViewerAsync (ct |> Some) fromPoint.Value |> TaskResult.map snd
                let! dp2 = distributionPointViewerAsync (ct |> Some) toPoint.Value |> TaskResult.map snd
                do!
                    (dp1.TenantId = tenantId && dp2.TenantId = tenantId)
                    |> Result.ofBool "Distribution point not found in tenant"

                if bookIds.Length = 0 then
                    return ()
                else
                    let setDistributionPointCommand: List<AggregateCommand<Book, BookEvent>> = 
                        [ 1 .. bookIds.Length ]
                        |>> fun _ -> BookCommand.SetDistributionPoint(toPoint, userId, DateTime.UtcNow)
                    let! result =
                        runNAggregateCommandsMdAsync<Book, BookEvent, string>
                            bookIds
                            eventStore
                            messageSenders
                            ""
                            setDistributionPointCommand
                            (Some ct)
                    return result
            }

    member this.SearchBooksByAuthorAsync(context: UserContext, authorId: AuthorId, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)

                let! author = authorViewerAsync (ct |> Some) authorId.Value |> TaskResult.map snd
                do!
                    (author.TenantId = tenantId)
                    |> Result.ofBool "Author not found in tenant"

                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct

                let filter (book: Book) = 
                    book.Authors |> List.contains authorId && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct  |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByAuthorsAsync(context: UserContext, authorsIds: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)

                let! authors =
                    authorsIds
                    |> List.traverseTaskResultM (fun a -> authorViewerAsync (ct |> Some) a.Value |> TaskResult.map snd) 

                do!
                    authors |> List.forall (fun a -> a.TenantId = tenantId)
                    |> Result.ofBool "Author not found in tenant"

                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct
                
                let filter (book: Book) = 
                    book.Authors |> List.exists (fun a -> authorsIds |> List.contains a) && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndAuthorsAsync(context: UserContext, title: Title, authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let filter (book: Book) = 
                    book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                    (book.Authors |> List.exists (fun a -> authors |> List.contains a)) && criteria.Invoke book && book.TenantId = tenantId

                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndAuthorsAndYearAsync(context: UserContext, title: Title, authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct
                let filter (book: Book) = 
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                    (book.Authors |> List.exists (fun a -> authors |> List.contains a)) &&
                    yearMatch && criteria.Invoke book && book.TenantId = tenantId


                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some) 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByAuthorsAndYearAsync(context: UserContext, authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct
                let filter (book: Book) = 
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    (book.Authors |> List.exists (fun a -> authors |> List.contains a)) &&
                    yearMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some) 
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByAuthorsAndCategoriesAsync(context: UserContext, authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct        
                let filter (book: Book) = 
                    let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                    let categoryMatch = 
                        categories |> Seq.exists (fun c -> 
                            book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                    authorMatch && categoryMatch && criteria.Invoke book && book.TenantId = tenantId
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndAuthorsAndCategoriesAsync(context: UserContext, title: Title, authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct        
                let filter (book: Book) = 
                    let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                    let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                    let categoryMatch = 
                        categories |> Seq.exists (fun c -> 
                            book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                    titleMatch && authorMatch && categoryMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByAuthorsAndYearAndCategoriesAsync(context: UserContext, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct        
                let filter (book: Book) = 
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                    let categoryMatch = 
                        categories |> Seq.exists (fun c -> 
                            book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                    yearMatch && authorMatch && categoryMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.SearchBooksByTitleAndAuthorsAndYearAndCategoriesAsync(context: UserContext, title: Title, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
        let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                do! 
                    checkIsGlobalAdminOrTenantManagerOrPublicTenant context ct        
                let filter (book: Book) = 
                    let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                    let yearMatch = 
                        match year with
                        | Before y -> book.Year.Value < y
                        | After y -> book.Year.Value > y
                        | Exact y -> book.Year.Value = y
                        | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                    let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                    let categoryMatch = 
                        categories |> Seq.exists (fun c -> 
                            book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                    titleMatch && yearMatch && authorMatch && categoryMatch && criteria.Invoke book && book.TenantId = tenantId
                        
                let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore (ct |> Some)
                return booksWithId |> List.ofSeq |> List.map snd
            }

    member this.LoanedByUserAtLeastOnceAsync (context: UserContext, bookId: BookId, userId: UserId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context)
                let! book =
                    bookViewerAsync (ct |> Some) bookId.Value |> TaskResult.map snd
                let! user =
                    userViewerAsync (ct |> Some) userId.Value |> TaskResult.map snd

                do! 
                    user.CurrentTenant = book.TenantId
                    |> Result.ofBool $"User tenant '{user.CurrentTenant}' doesn't match book tenant '{book.TenantId}'"
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool $"Book tenant '{book.TenantId}' doesn't match user tenant '{tenantId}'"
                let! loans =
                    StateView.getAllFilteredAggregateStatesAsync<Loan, LoanEvent, string> 
                        (fun loan -> loan.BookId = bookId && loan.UserId = userId && loan.LoanStatus.IsReturned)
                        eventStore
                        (Some ct)
                    |> TaskResult.map (fun x -> x |> List.ofSeq |> List.map snd)
                return not loans.IsEmpty
            }

    interface IBookService with                
        member this.AddAuthorToBookAsync(context: UserContext, authorId: AuthorId, bookId: BookId, ?ct: CancellationToken ) =
            let ct = defaultArg ct CancellationToken.None
            let dateTime = System.DateTime.Now
            this.AddAuthorToBookAsync(context, authorId, bookId, dateTime, ct)
        member this.AddBookAsync(context: UserContext, book: Book, ?ct: CancellationToken ) =
            let ct = defaultArg ct CancellationToken.None
            this.AddBookAsync(context, book, ct)
        member this.AddBooksAsync(context: UserContext, books: List<Book>, ?ct: CancellationToken ) =
            let ct = defaultArg ct CancellationToken.None
            this.AddBooksAsync(context, books, ct)
        member this.RemoveAuthorFromBookAsync(context: UserContext, authorId: AuthorId, bookId: BookId, ?ct: CancellationToken ) =
            let ct = defaultArg ct CancellationToken.None
            let dateTime = System.DateTime.Now
            this.RemoveAuthorFromBookAsync(context, authorId, bookId, dateTime, ct)
        member this.RemoveBookAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken ) =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveBookAsync(context, bookId, ct)
        member this.GetBookAsync(context: UserContext, id: BookId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetBookAsync(context, id, ct)
        member this.GetBooksAsync(context: UserContext, bookIds: List<BookId>, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetBooksAsync(context, bookIds, ct)
        member this.GetAllAsync(context: UserContext, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.GetAllBooksAsync(context, criteria, ct)
        member this.GetAllBooksOfTenantAsync(context: UserContext, tenantId: TenantId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetAllBooksOfTenantAsync(context, tenantId, ct)
        member this.SearchByTitleAsync(context: UserContext, title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAsync(context, title, criteria, ct)
        member this.SearchByIsbnAsync(context: UserContext, isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByIsbnAsync(context, isbn, criteria, ct)
        member this.SearchByTitleAndIsbnAsync(context: UserContext, title: Title, isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndIsbnAsync(context, title, isbn, criteria, ct)
        member this.ChangeMainCategoryAsync(context: UserContext, category: Category, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.ChangeMainCategoryAsync(context, category, bookId, ct)
        member this.AddAdditionalCategoryAsync(context: UserContext, category: Category, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.AddAdditionalCategoryAsync(context, category, bookId, ct)
        member this.RemoveAdditionalCategoryAsync(context: UserContext, category: Category, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveAdditionalCategoryAsync(context, category, bookId, ct)
        member this.UpdateTitleAsync(context: UserContext, title: Title, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateTitleAsync(context, title, bookId, ct)
        member this.UpdateDescriptionAsync(context: UserContext, description: string, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateDescriptionAsync(context, description, bookId, ct)
        member this.RemoveDescriptionAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveDescriptionAsync(context, bookId, ct)
        member this.EmbedDescriptionAsync(context: UserContext, bookId: BookId, embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.EmbedDescriptionAsync(context, bookId, embeddingDataId, ct)
        member this.RemoveEmbeddingAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveEmbeddingAsync(context, bookId, ct)
        member this.ForceBulkRemoveEmbeddingsAsync(context: UserContext, bookIds: List<BookId>, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.ForceBulkRemoveEmbeddingsAsync(context, bookIds, ct)
        member this.UpdateIsbnAsync(context: UserContext, isbn: Isbn, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateIsbnAsync(context, isbn, bookId, ct)
        member this.UnsealAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UnsealAsync(context, bookId, ct)
        member this.SealAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.SealAsync(context, bookId, ct)
        member this.SearchByYearAsync(context: UserContext, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByYearAsync(context, year, criteria, ct)
        member this.SearchByTitleAndYearAsync(context: UserContext, title: Title, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndYearAsync(context, title, year, criteria, ct)
        member this.SearchByIsbnAndYearAsync(context: UserContext, isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByIsbnAndYearAsync(context, isbn, year, criteria, ct)
        member this.SearchByTitleAndIsbnAndYearAsync(context: UserContext, title: Title, isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndIsbnAndYearAsync(context, title, isbn, year, criteria, ct)
        member this.SearchByCategoriesAsync(context: UserContext, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByCategoriesAsync(context, categories, criteria, ct)
        member this.SearchByTitleAndCategoriesAsync(context: UserContext, title: Title, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndCategoriesAsync(context, title, categories, criteria, ct)
        member this.SearchByYearAndCategoriesAsync(context: UserContext, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByYearAndCategoriesAsync(context, year, categories, criteria, ct)
        member this.SearchByTitleAndYearAndCategoriesAsync(context: UserContext, title: Title, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndYearAndCategoriesAsync(context, title, year, categories, criteria, ct)
        member this.SearchByIsbnOrTitleAsync(context: UserContext, isbn: Isbn, title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByIsbnOrTitleAsync(context, isbn, title, criteria, ct)
        member this.SearchByAuthorAsync(context: UserContext, authorId: AuthorId, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByAuthorAsync(context, authorId, criteria, ct)
        member this.SearchByAuthorsAsync(context: UserContext, authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByAuthorsAsync(context, authors, criteria, ct)
        member this.SearchByAuthorsAndYearAsync(context: UserContext, authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByAuthorsAndYearAsync(context, authors, year, criteria, ct)
        member this.SearchByTitleAndAuthorsAsync(context: UserContext, title: Title, authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndAuthorsAsync(context, title, authors, criteria, ct)
        member this.SearchByTitleAndAuthorsAndYearAsync(context: UserContext, title: Title, authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndAuthorsAndYearAsync(context, title, authors, year, criteria, ct)
        member this.SearchByAuthorsAndCategoriesAsync(context: UserContext, authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByAuthorsAndCategoriesAsync(context, authors, categories, criteria, ct)
        member this.SearchByTitleAndAuthorsAndCategoriesAsync(context: UserContext, title: Title, authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndAuthorsAndCategoriesAsync(context, title, authors, categories, criteria, ct)
        member this.SearchByAuthorsAndYearAndCategoriesAsync(context: UserContext, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByAuthorsAndYearAndCategoriesAsync(context, authors, year, categories, criteria, ct)
        member this.SearchByTitleAndAuthorsAndYearAndCategoriesAsync(context: UserContext, title: Title, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
            let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
            let ct = defaultArg ct CancellationToken.None
            this.SearchBooksByTitleAndAuthorsAndYearAndCategoriesAsync(context, title, authors, year, categories, criteria, ct)
        member this.RemoveImageUrlAsync(context: UserContext, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveImageUrlAsync(context, bookId, ct)
        member this.SetImageUrlAsync(context: UserContext, bookId: BookId, imageUrl: Uri, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.SetImageUrlAsync(context, bookId, imageUrl, ct)
        member this.SetAvailabilityAsync(context: UserContext, availability: Availability, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.SetAvailabilityAsync(context, availability, bookId, ct)
        member this.BulkEditAsync(context: UserContext, bookIds: List<BookId>, editCriteria: BulkBookEdit, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.BulkEditAsync(context, bookIds, editCriteria, ct)
        member this.LoanedByUserAtLeastOnceAsync(context: UserContext, bookId: BookId, userId: UserId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.LoanedByUserAtLeastOnceAsync(context, bookId, userId, ct)
        member this.AddTagToBookAsync(context: UserContext, tag: Tag, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.AddTagToBookAsync(context, tag, bookId, ct)
        member this.RemoveTagFromBookAsync(context: UserContext, tag: Tag, bookId: BookId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveTagFromBookAsync(context, tag, bookId, ct)
        member this.SetDistributionPointAsync(context, distributionPointId, bookId, userId, ?ct) = 
            let ct = defaultArg ct CancellationToken.None
            this.SetDistributionPointAsync(context, distributionPointId, bookId, userId, ct)
        member this.UnSetDistributionPointAsync(context, distributionPointId, bookId, userId, ?ct) = 
            let ct = defaultArg ct CancellationToken.None
            this.UnSetDistributionPointAsync(context, distributionPointId, bookId, userId, ct)
        member this.UnsetAllBookRelatedToDPAsync(context, distributionPointId, userId, ?ct) = 
            let ct = defaultArg ct CancellationToken.None
            this.UnsetAllBookRelatedToDPAsync(context, distributionPointId, userId, ct)
        member this.MoveFromDpToAnotherDPAsync(context, fromPoint, toPoint, userId, ?ct) = 
            let ct = defaultArg ct CancellationToken.None
            this.MoveFromDpToAnotherDPAsync(context, fromPoint, toPoint, userId, ct)
