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
        reservationService: IReservationService
    ) =

    new (eventStore: IEventStore<string>, reservationService: IReservationService)
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore

        BookService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            reservationService
        )
    new (configuration: IConfiguration, reservationService: IReservationService)
        =
        let connectionString = configuration.GetConnectionString("BookLibraryDbConnection")
        let eventStore = PgStorage.PgEventStore connectionString
        BookService(eventStore, reservationService)

    member private 
        this.GetRefreshableBookDetailsAsync(bookId: BookId, ?ct:CancellationToken): TaskResult<RefreshableBookDetails, string> =
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let refresher =
                        fun () ->
                            result {
                                let ct = ct |> Option.defaultValue CancellationToken.None
                                let! book = 
                                    bookViewerAsync (ct |> Some) bookId.Value 
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                    |> Result.map snd
                                let! currentLoan = 
                                    match book.CurrentLoan with
                                    | Some loanId -> 
                                        let loan = 
                                            loanViewerAsync (Some ct) loanId.Value |> TaskResult.map snd
                                            |> Async.AwaitTask
                                            |> Async.RunSynchronously
                                        match loan with
                                        | Ok loan -> loan |> Some |> Ok
                                        | Error x ->  Error x
                                    | None -> 
                                        None |> Ok
                                let! authors = 
                                    book.Authors
                                    |> List.traverseTaskResultM (fun authorId -> authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let! futureReservations = 
                                    book.CurrentReservations
                                    |> List.traverseTaskResultM (fun reservationId -> reservationService.GetReservationDetailsAsync (reservationId, ct))
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                    
                                return 
                                    { 
                                        Authors = authors
                                        Book = book
                                        CurrentLoan = currentLoan
                                        ReservationsDetails = futureReservations
                                    } 
                            }
                    result {
                        let! bookDetails = refresher ()
                        return 
                            { 
                                BookDetails = bookDetails
                                Refresher = refresher
                            } :> Refreshable<RefreshableBookDetails>
                            ,
                            bookId.Value :: 
                            (if bookDetails.CurrentLoan.IsSome then [bookDetails.CurrentLoan.Value.LoanId.Value] else []) @ 
                            (bookDetails.ReservationsDetails |> List.map _.Reservation.ReservationId.Value) @
                            (bookDetails.Authors |> List.map _.AuthorId.Value)
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableBookDetails> bookId.Value
            task {
                return StateView.getRefreshableDetailsAsync<RefreshableBookDetails> (fun ct -> detailsBuilder ct) key ct
            }
                
            member this.AddBookAsync (book: Book, ?ct: CancellationToken) =
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! authors: List<Author> = 
                            book.Authors
                            |> List.traverseTaskResultM 
                                (fun authorId -> authorViewerAsync (Some ct) authorId.Value  |> TaskResult.map snd )

                        let authorAddBooks: List<AggregateCommand<Author, AuthorEvent>> = 
                            authors
                            |> List.map (fun _ -> AuthorCommand.AddBook book.BookId)

                        return!
                            runInitAndNAggregateCommandsMdAsync<Author, AuthorEvent, Book, string>
                            (book.Authors |>> (fun authorId -> authorId.Value))
                            eventStore
                            messageSenders
                            book
                            ""
                            authorAddBooks
                            (Some ct)
                    }

            member this.AddBooksAsync (books: List<Book>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! result =
                            books
                            |> List.traverseTaskResultM (fun book -> this.AddBookAsync(book, ct))
                        return () 
                    }

            member this.RemoveBookAsync (bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                        let! result = 
                            runDeleteAsync<Book, BookEvent, string>
                                eventStore
                                messageSenders
                                bookId.Value
                                (fun Book -> Book.CurrentLoan.IsNone && Book.CurrentReservations.Length = 0)
                                (Some ct)
                        return result
                    }

            member this.AddAuthorToBookAsync (authorId: AuthorId, bookId: BookId, dateTime: System.DateTime, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd

                        let! author =
                            authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd

                        let bookAddAuthorCommand = 
                            BookCommand.AddAuthor (authorId, dateTime)

                        let authorAddBookCommand: AggregateCommand<Author, AuthorEvent> = 
                            AuthorCommand.AddBook book.BookId
                        let! result = 
                            runTwoNAggregateCommandsMdAsync<Book, BookEvent, Author, AuthorEvent, string>
                                [book.Id]
                                [author.Id]
                                eventStore
                                messageSenders
                                ""
                                [bookAddAuthorCommand]
                                [authorAddBookCommand]
                                (Some ct)
                        return result
                    }

            member this.UpdateTitleAsync (title: Title, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
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

            member this.UpdateDescriptionAsync (description: string, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
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

            member this.RemoveDescriptionAsync (bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                        let dateTime = System.DateTime.UtcNow
                        let bookRemoveDescriptionCommand = 
                            BookCommand.RemoveDescription (dateTime)
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

            member this.UpdateIsbnAsync (isbn: Isbn, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                        let dateTime = System.DateTime.UtcNow
                        let bookUpdateIsbnCommand = 
                            BookCommand.UpdateIsbn (isbn, dateTime)
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

            member this.RemoveImageUrlAsync (bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
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

            member this.SetImageUrlAsync (bookId: BookId, imageUrl: Uri, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
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
            member this.SetAvailabilityAsync (availability: Availability, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
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

            member this.BulkEditAsync (bookIds: List<BookId>, bulkBookEdit: BulkBookEdit, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                taskResult
                    {
                        let dateTime = System.DateTime.UtcNow
                        let preExecutedYearEditCommands = 
                            match bulkBookEdit.YearEdit with
                            | Some year -> 
                                let command = BookCommand.UpdateYear (year, dateTime)
                                let! preExecutedYearUpdateCommands =
                                    bookIds
                                    |> List.map _.Value
                                    |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                                if preExecutedYearUpdateCommands.IsError then
                                    let (Error e) = preExecutedYearUpdateCommands
                                    printf "Error pre-executing year update command: %A\n" e
                                    None 
                                else
                                    Some preExecutedYearUpdateCommands.OkValue
                            | None -> None

                        let preExecutedMainCategoryCommands =
                            match bulkBookEdit.MainCategoryEdit with
                            | Some mainCategory -> 
                                let command = BookCommand.ChangeMainCategory (mainCategory, dateTime)
                                let! preExecutedMainCategoryUpdateCommands =
                                    bookIds
                                    |> List.map _.Value
                                    |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                                if preExecutedMainCategoryUpdateCommands.IsError then
                                    let (Error e) = preExecutedMainCategoryUpdateCommands
                                    printf "Error pre-executing main category update command: %A\n" e
                                    None 
                                else
                                    Some preExecutedMainCategoryUpdateCommands.OkValue
                            | None -> None

                        let preExecutedAdditionalCategory =
                            match bulkBookEdit.AdditionalCategoriesEdit with
                            | Some additionalCategories -> 
                                let command = BookCommand.ReplaceAdditionalCategories (additionalCategories, dateTime)
                                let! preExecutedAdditionalCategoryUpdateCommands =
                                    bookIds
                                    |> List.map _.Value
                                    |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                                if preExecutedAdditionalCategoryUpdateCommands.IsError then
                                    let (Error e) = preExecutedAdditionalCategoryUpdateCommands
                                    printf "Error pre-executing additional category update command: %A\n" e
                                    None 
                                else
                                    Some preExecutedAdditionalCategoryUpdateCommands.OkValue
                            | None -> None

                        let preExecutedAvailabilityEditCommands =
                            match bulkBookEdit.AvailabilityEdit with
                            | Some availability -> 
                                let command = BookCommand.SetAvailability (availability, dateTime)
                                let! preExecutedAvailabilityUpdateCommands =
                                    bookIds
                                    |> List.map _.Value
                                    |> List.traverseResultM (fun id -> preExecuteAggregateCommandMd<Book, BookEvent, string> id eventStore MessageSenders.NoSender "" command)
                                if preExecutedAvailabilityUpdateCommands.IsError then
                                    let (Error e) = preExecutedAvailabilityUpdateCommands
                                    printf "Error pre-executing availability update command: %A\n" e
                                    None 
                                else
                                    Some preExecutedAvailabilityUpdateCommands.OkValue
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
                        let result = 
                            runPreExecutedAggregateCommands<string>      
                                allPreExecutedCommands
                                eventStore
                                messageSenders
                        return! result
                    }

            member this.RemoveAuthorFromBookAsync (authorId: AuthorId, bookId: BookId, dateTime: System.DateTime, ?ct: CancellationToken) = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) bookId.Value |> TaskResult.map snd
                        let! author = 
                            authorViewerAsync (Some ct) authorId.Value |> TaskResult.map snd

                        let bookRemoveAuthorCommand = 
                            BookCommand.RemoveAuthor (authorId, dateTime)
                        let authorRemoveBookCommand: AggregateCommand<Author, AuthorEvent> = 
                            AuthorCommand.RemoveBook book.BookId
                        let result = 
                            runTwoNAggregateCommandsMdAsync<Book, BookEvent, Author, AuthorEvent, string>
                                [book.Id]
                                [authorId.Value]
                                eventStore
                                messageSenders
                                ""
                                [bookRemoveAuthorCommand]
                                [authorRemoveBookCommand]
                                (Some ct)
                        return! result
                    }

            member this.GetBookAsync (id: BookId, ?ct: CancellationToken): Task<Result<Book, string>> = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! result = 
                            bookViewerAsync (Some ct) id.Value 
                        return result |> snd
                    }

            member this.GetAllBooksAsync(?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> (fun b -> criteria.Invoke b) eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAsync(title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) && criteria.Invoke book
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByIsbnAsync(isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase) && criteria.Invoke book
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndIsbnAsync(title: Title, isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            ((String.IsNullOrWhiteSpace(book.Title.Value) |> not && String.IsNullOrWhiteSpace(title.Value) |> not && book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)) || 
                            (String.IsNullOrWhiteSpace(book.Isbn.Value) |> not && String.IsNullOrWhiteSpace(isbn.Value) |> not && book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)))
                            && criteria.Invoke book

                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByYearAsync(year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            match year with
                            | Before y -> book.Year.Value < y
                            | After y -> book.Year.Value > y
                            | Exact y -> book.Year.Value = y
                            | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2

                        let compoundFilter = fun (book: Book) -> 
                            filter book && criteria.Invoke book

                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> compoundFilter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndYearAsync(title: Title, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                            let yearMatch = 
                                match year with
                                | Before y -> book.Year.Value < y
                                | After y -> book.Year.Value > y
                                | Exact y -> book.Year.Value = y
                                | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                            titleMatch && yearMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByIsbnAndYearAsync(isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let isbnMatch = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)
                            let yearMatch = 
                                match year with
                                | Before y -> book.Year.Value < y
                                | After y -> book.Year.Value > y
                                | Exact y -> book.Year.Value = y
                                | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                            isbnMatch && yearMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndIsbnAndYearAsync(title: Title, isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                            let isbnMatch = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)
                            let yearMatch = 
                                match year with
                                | Before y -> book.Year.Value < y
                                | After y -> book.Year.Value > y
                                | Exact y -> book.Year.Value = y
                                | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                            titleMatch && isbnMatch && yearMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByCategoriesAsync(categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            (categories |> Seq.exists (fun c -> 
                                book.MainCategory = c || (book.AdditionalCategories |> List.contains c)))
                            && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndCategoriesAsync(title: Title, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                            (categories |> Seq.exists (fun c -> 
                                book.MainCategory = c || (book.AdditionalCategories |> List.contains c)))
                            && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByYearAndCategoriesAsync(year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
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
                            yearMatch && categoryMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndYearAndCategoriesAsync(title: Title, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
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
                            titleMatch && yearMatch && categoryMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByIsbnOrTitleAsync(isbn: Isbn, title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            (book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase) ||
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase))
                            && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                }

            member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken): TaskResult<BookDetails, string> = 
                taskResult {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableBookDetails =
                        this.GetRefreshableBookDetailsAsync(bookId, ct)
                    return refreshableBookDetails.BookDetails
                }

            member this.GetBooksDetailsAsync(bookIds: List<BookId>, ?ct: CancellationToken) = 
                taskResult {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableBookDetails = 
                        bookIds
                        |> List.traverseTaskResultM (fun bookId -> this.GetRefreshableBookDetailsAsync(bookId, ct))
                    return refreshableBookDetails |>> (fun x -> x.BookDetails)
                }

            member this.ChangeMainCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let command = BookCommand.ChangeMainCategory (category, System.DateTime.Now)
                        return!
                            runAggregateCommandMdAsync<Book, BookEvent, string>
                                bookId.Value
                                eventStore
                                messageSenders
                                ""
                                command
                                ct
                    }
            member this.AddAdditionalCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let command = BookCommand.AddAdditionalCategory (category, System.DateTime.Now)
                        return!
                            runAggregateCommandMdAsync<Book, BookEvent, string>
                                bookId.Value
                                eventStore
                                messageSenders
                                ""
                                command
                                ct
                    }

            member this.RemoveAdditionalCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let command = BookCommand.RemoveAdditionalCategory (category, System.DateTime.Now)
                        return!
                            runAggregateCommandMdAsync<Book, BookEvent, string>
                                bookId.Value
                                eventStore
                                messageSenders
                                ""
                                command
                                ct
                    }
            member this.SealAsync(bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let command = BookCommand.Seal (System.DateTime.UtcNow)
                        return!
                            runAggregateCommandMdAsync<Book, BookEvent, string>
                                bookId.Value
                                eventStore
                                messageSenders
                                ""
                                command
                                ct
                    }
            member this.UnsealAsync(bookId: BookId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let command = BookCommand.Unseal (System.DateTime.Now)
                        return!
                            runAggregateCommandMdAsync<Book, BookEvent, string>
                                bookId.Value
                                eventStore
                                messageSenders
                                ""
                                command
                                ct
                    }

            member this.SearchBooksByAuthorAsync(authorId: AuthorId, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Authors |> List.contains authorId && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAsync(authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Authors |> List.exists (fun a -> authors |> List.contains a) && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAsync(title: Title, authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                            (book.Authors |> List.exists (fun a -> authors |> List.contains a)) && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAndYearAsync(title: Title, authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let yearMatch = 
                                match year with
                                | Before y -> book.Year.Value < y
                                | After y -> book.Year.Value > y
                                | Exact y -> book.Year.Value = y
                                | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                            (book.Authors |> List.exists (fun a -> authors |> List.contains a)) &&
                            yearMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAndYearAsync(authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let yearMatch = 
                                match year with
                                | Before y -> book.Year.Value < y
                                | After y -> book.Year.Value > y
                                | Exact y -> book.Year.Value = y
                                | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                            (book.Authors |> List.exists (fun a -> authors |> List.contains a)) &&
                            yearMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAndCategoriesAsync(authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                            let categoryMatch = 
                                categories |> Seq.exists (fun c -> 
                                    book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                            authorMatch && categoryMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAndCategoriesAsync(title: Title, authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
                        let filter (book: Book) = 
                            let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                            let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                            let categoryMatch = 
                                categories |> Seq.exists (fun c -> 
                                    book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                            titleMatch && authorMatch && categoryMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAndYearAndCategoriesAsync(authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
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
                            yearMatch && authorMatch && categoryMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAndYearAndCategoriesAsync(title: Title, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                taskResult
                    {
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
                            titleMatch && yearMatch && authorMatch && categoryMatch && criteria.Invoke book
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

        interface IBookService with                
            member this.AddAuthorToBookAsync(authorId: AuthorId, bookId: BookId, ?ct: CancellationToken ) =
                let ct = defaultArg ct CancellationToken.None
                let dateTime = System.DateTime.Now
                this.AddAuthorToBookAsync(authorId, bookId, dateTime, ct)
            member this.AddBookAsync(book: Book, ?ct: CancellationToken ) =
                let ct = defaultArg ct CancellationToken.None
                this.AddBookAsync(book, ct)
            member this.AddBooksAsync(books: List<Book>, ?ct: CancellationToken ) =
                let ct = defaultArg ct CancellationToken.None
                this.AddBooksAsync(books, ct)
            member this.GetBookAsync(id: BookId, ?ct: CancellationToken) =
                let ct = defaultArg ct CancellationToken.None
                this.GetBookAsync(id, ct)
            member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.GetBookDetailsAsync(bookId, ct)
            member this.GetBooksDetailsAsync(bookIds: List<BookId>, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.GetBooksDetailsAsync(bookIds, ct)
            member this.GetAllAsync(?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.GetAllBooksAsync(criteria, ct)
            member this.RemoveAuthorFromBookAsync(authorId: AuthorId, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                let dateTime = System.DateTime.Now
                this.RemoveAuthorFromBookAsync(authorId, bookId, dateTime, ct)
            member this.SearchByTitleAsync(title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAsync(title, criteria, ct)
            member this.SearchByIsbnAsync(isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByIsbnAsync(isbn, criteria, ct)
            member this.SearchByTitleAndIsbnAsync(title: Title, isbn: Isbn, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndIsbnAsync(title, isbn, criteria, ct)
            member this.ChangeMainCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.ChangeMainCategoryAsync(category, bookId, ct)
            member this.AddAdditionalCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.AddAdditionalCategoryAsync(category, bookId, ct)
            member this.RemoveAdditionalCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.RemoveAdditionalCategoryAsync(category, bookId, ct)
            member this.SealAsync(bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.SealAsync(bookId, ct)
            member this.UnsealAsync(bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.UnsealAsync(bookId, ct)
            member this.RemoveBookAsync(bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.RemoveBookAsync(bookId, ct)
            member this.UpdateTitleAsync(title: Title, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.UpdateTitleAsync(title, bookId, ct)
            member this.UpdateDescriptionAsync(description: string, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.UpdateDescriptionAsync(description, bookId, ct)
            member this.RemoveDescriptionAsync(bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.RemoveDescriptionAsync(bookId, ct)
            member this.UpdateIsbnAsync(isbn: Isbn, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.UpdateIsbnAsync(isbn, bookId, ct)
            member this.SearchByYearAsync(year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByYearAsync(year, criteria, ct)
            member this.SearchByTitleAndYearAsync(title: Title, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndYearAsync(title, year, criteria, ct)
            member this.SearchByIsbnAndYearAsync(isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByIsbnAndYearAsync(isbn, year, criteria, ct)
            member this.SearchByTitleAndIsbnAndYearAsync(title: Title, isbn: Isbn, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndIsbnAndYearAsync(title, isbn, year, criteria, ct)
            member this.SearchByCategoriesAsync(categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByCategoriesAsync(categories, criteria, ct)
            member this.SearchByTitleAndCategoriesAsync(title: Title, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndCategoriesAsync(title, categories, criteria, ct)
            member this.SearchByYearAndCategoriesAsync(year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByYearAndCategoriesAsync(year, categories, criteria, ct)
            member this.SearchByTitleAndYearAndCategoriesAsync(title: Title, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndYearAndCategoriesAsync(title, year, categories, criteria, ct)
            member this.SearchByIsbnOrTitleAsync(isbn: Isbn, title: Title, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByIsbnOrTitleAsync(isbn, title, criteria, ct)
            member this.SearchByAuthorAsync(authorId: AuthorId, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByAuthorAsync(authorId, criteria, ct)
            member this.SearchByAuthorsAsync(authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByAuthorsAsync(authors, criteria, ct)
            member this.SearchByAuthorsAndYearAsync(authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByAuthorsAndYearAsync(authors, year, criteria, ct)
            member this.SearchByTitleAndAuthorsAsync(title: Title, authors: List<AuthorId>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndAuthorsAsync(title, authors, criteria, ct)
            member this.SearchByTitleAndAuthorsAndYearAsync(title: Title, authors: List<AuthorId>, year: YearSearch, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndAuthorsAndYearAsync(title, authors, year, criteria, ct)
            member this.SearchByAuthorsAndCategoriesAsync(authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByAuthorsAndCategoriesAsync(authors, categories, criteria, ct)
            member this.SearchByTitleAndAuthorsAndCategoriesAsync(title: Title, authors: List<AuthorId>, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndAuthorsAndCategoriesAsync(title, authors, categories, criteria, ct)
            member this.SearchByAuthorsAndYearAndCategoriesAsync(authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByAuthorsAndYearAndCategoriesAsync(authors, year, categories, criteria, ct)
            member this.SearchByTitleAndAuthorsAndYearAndCategoriesAsync(title: Title, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?criteria: BookSearchCriteria, ?ct: CancellationToken) = 
                let criteria = defaultArg (criteria |> Option.bind Option.ofObj) SearchCriteria.searchAllBooks
                let ct = defaultArg ct CancellationToken.None
                this.SearchBooksByTitleAndAuthorsAndYearAndCategoriesAsync(title, authors, year, categories, criteria, ct)
            member this.RemoveImageUrlAsync(bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.RemoveImageUrlAsync(bookId, ct)
            member this.SetImageUrlAsync(bookId: BookId, imageUrl: Uri, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.SetImageUrlAsync(bookId, imageUrl, ct)
            member this.SetAvailabilityAsync(availability: Availability, bookId: BookId, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.SetAvailabilityAsync(availability, bookId, ct)

            member this.BulkEditAsync(bookIds: List<BookId>, editCriteria: BulkBookEdit, ?ct: CancellationToken) = 
                let ct = defaultArg ct CancellationToken.None
                this.BulkEditAsync(bookIds, editCriteria, ct)
                    
