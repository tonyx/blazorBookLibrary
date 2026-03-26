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

type BookService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>
    ) =

    new (eventStore: IEventStore<string>)
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        BookService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync
        )
    new (connectionString: string)
        =
        let eventStore = PgStorage.PgEventStore connectionString
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        BookService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync
        )
    new (configuration: IConfiguration)
        =
        let connectionString = configuration.GetConnectionString("BookLibraryDbConnection")
        BookService(connectionString)
    member private this.GetRefreshableBookDetailsAsync(bookId: BookId, ?ct:CancellationToken): TaskResult<RefreshableBookDetails, string> =
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
                                    |> List.traverseTaskResultM (fun reservationId -> reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                    
                                return 
                                    { 
                                        Authors = authors
                                        Book = book
                                        CurrentLoan = currentLoan
                                        FutureReservations = futureReservations
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
                            (bookDetails.FutureReservations |> List.map _.ReservationId.Value) @
                            (bookDetails.Authors |> List.map _.AuthorId.Value)
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableBookDetails> bookId.Value
            task {
                return StateView.getRefreshableDetailsAsync<RefreshableBookDetails> (fun ct -> detailsBuilder ct) key ct
            }
                
            member this.AddBookAsync (book: Book, ?ct: CancellationToken) =
                let ct = defaultArg ct CancellationToken.None
                taskResult
                    {
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

            member this.GetAllBooksAsync(?ct: CancellationToken) = 
                taskResult
                    {
                        let! booksWithId = StateView.getAllAggregateStatesAsync<Book, BookEvent, string> eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAsync(title: Title, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByIsbnAsync(isbn: Isbn, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase)
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndIsbnAsync(title: Title, isbn: Isbn, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            (String.IsNullOrWhiteSpace(book.Title.Value) |> not && String.IsNullOrWhiteSpace(title.Value) |> not && book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)) || 
                            (String.IsNullOrWhiteSpace(book.Isbn.Value) |> not && String.IsNullOrWhiteSpace(isbn.Value) |> not && book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase))

                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByYearAsync(year: YearSearch, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            match year with
                            | Before y -> book.Year.Value < y
                            | After y -> book.Year.Value > y
                            | Exact y -> book.Year.Value = y
                            | Range (y1, y2) -> book.Year.Value >= y1 && book.Year.Value <= y2
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndYearAsync(title: Title, year: YearSearch, ?ct: CancellationToken) = 
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
                            titleMatch && yearMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByIsbnAndYearAsync(isbn: Isbn, year: YearSearch, ?ct: CancellationToken) = 
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
                            isbnMatch && yearMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndIsbnAndYearAsync(title: Title, isbn: Isbn, year: YearSearch, ?ct: CancellationToken) = 
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
                            titleMatch && isbnMatch && yearMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByCategoriesAsync(categories: List<Category>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            categories |> Seq.exists (fun c -> 
                                book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndCategoriesAsync(title: Title, categories: List<Category>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                            categories |> Seq.exists (fun c -> 
                                book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByYearAndCategoriesAsync(year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
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
                            yearMatch && categoryMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndYearAndCategoriesAsync(title: Title, year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
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
                            titleMatch && yearMatch && categoryMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByIsbnOrTitleAsync(isbn: Isbn, title: Title, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Isbn.Value.Contains(isbn.Value, StringComparison.OrdinalIgnoreCase) ||
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                        
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
                        let command = BookCommand.Seal (System.DateTime.Now)
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

            member this.SearchBooksByAuthorAsync(authorId: AuthorId, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Authors |> List.contains authorId
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAsync(authors: List<AuthorId>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Authors |> List.exists (fun a -> authors |> List.contains a)
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAsync(title: Title, authors: List<AuthorId>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase) &&
                            book.Authors |> List.exists (fun a -> authors |> List.contains a)
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAndYearAsync(title: Title, authors: List<AuthorId>, year: YearSearch, ?ct: CancellationToken) = 
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
                            yearMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAndYearAsync(authors: List<AuthorId>, year: YearSearch, ?ct: CancellationToken) = 
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
                            yearMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAndCategoriesAsync(authors: List<AuthorId>, categories: List<Category>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                            let categoryMatch = 
                                categories |> Seq.exists (fun c -> 
                                    book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                            authorMatch && categoryMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAndCategoriesAsync(title: Title, authors: List<AuthorId>, categories: List<Category>, ?ct: CancellationToken) = 
                taskResult
                    {
                        let filter (book: Book) = 
                            let titleMatch = book.Title.Value.Contains(title.Value, StringComparison.OrdinalIgnoreCase)
                            let authorMatch = (book.Authors |> List.exists (fun a -> authors |> List.contains a))
                            let categoryMatch = 
                                categories |> Seq.exists (fun c -> 
                                    book.MainCategory = c || (book.AdditionalCategories |> List.contains c))
                            titleMatch && authorMatch && categoryMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByAuthorsAndYearAndCategoriesAsync(authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
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
                            yearMatch && authorMatch && categoryMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

            member this.SearchBooksByTitleAndAuthorsAndYearAndCategoriesAsync(title: Title, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
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
                            titleMatch && yearMatch && authorMatch && categoryMatch
                        
                        let! booksWithId = StateView.getAllFilteredAggregateStatesAsync<Book, BookEvent, string> filter eventStore ct 
                        return booksWithId |> List.ofSeq |> List.map snd
                    }

        interface IBookService with                
            member this.AddAuthorToBookAsync(authorId: AuthorId, bookId: BookId, ?ct: CancellationToken) =
                let dateTime = System.DateTime.Now
                this.AddAuthorToBookAsync(authorId, bookId, dateTime, ct |> Option.defaultValue CancellationToken.None)
            member this.AddBookAsync(book: Book, ?ct: CancellationToken ) =
                this.AddBookAsync(book, ct |> Option.defaultValue CancellationToken.None)
            member this.GetBookAsync(id: BookId, ?ct: CancellationToken) =
                this.GetBookAsync(id, ct |> Option.defaultValue CancellationToken.None)
            member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken) = 
                this.GetBookDetailsAsync(bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.GetBooksDetailsAsync(bookIds: List<BookId>, ?ct: CancellationToken) = 
                this.GetBooksDetailsAsync(bookIds, ct |> Option.defaultValue CancellationToken.None)
            member this.GetAllAsync(?ct: CancellationToken) = 
                this.GetAllBooksAsync(ct |> Option.defaultValue CancellationToken.None)
            member this.RemoveAuthorFromBookAsync(authorId: AuthorId, bookId: BookId, ?ct: CancellationToken) = 
                let dateTime = System.DateTime.Now
                this.RemoveAuthorFromBookAsync(authorId, bookId, dateTime, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAsync(title: Title, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAsync(title, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByIsbnAsync(isbn: Isbn, ?ct: CancellationToken) = 
                this.SearchBooksByIsbnAsync(isbn, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndIsbnAsync(title: Title, isbn: Isbn, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndIsbnAsync(title, isbn, ct |> Option.defaultValue CancellationToken.None)
            member this.ChangeMainCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                this.ChangeMainCategoryAsync(category, bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.AddAdditionalCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                this.AddAdditionalCategoryAsync(category, bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.RemoveAdditionalCategoryAsync(category: Category, bookId: BookId, ?ct: CancellationToken) = 
                this.RemoveAdditionalCategoryAsync(category, bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.SealAsync(bookId: BookId, ?ct: CancellationToken) = 
                this.SealAsync(bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.UnsealAsync(bookId: BookId, ?ct: CancellationToken) = 
                this.UnsealAsync(bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByYearAsync(year: YearSearch, ?ct: CancellationToken) = 
                this.SearchBooksByYearAsync(year, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndYearAsync(title: Title, year: YearSearch, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndYearAsync(title, year, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByIsbnAndYearAsync(isbn: Isbn, year: YearSearch, ?ct: CancellationToken) = 
                this.SearchBooksByIsbnAndYearAsync(isbn, year, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndIsbnAndYearAsync(title: Title, isbn: Isbn, year: YearSearch, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndIsbnAndYearAsync(title, isbn, year, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByCategoriesAsync(categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByCategoriesAsync(categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndCategoriesAsync(title: Title, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndCategoriesAsync(title, categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByYearAndCategoriesAsync(year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByYearAndCategoriesAsync(year, categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndYearAndCategoriesAsync(title: Title, year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndYearAndCategoriesAsync(title, year, categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByIsbnOrTitleAsync(isbn: Isbn, title: Title, ?ct: CancellationToken) = 
                this.SearchBooksByIsbnOrTitleAsync(isbn, title, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByAuthorAsync(authorId: AuthorId, ?ct: CancellationToken) = 
                this.SearchBooksByAuthorAsync(authorId, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByAuthorsAsync(authors: List<AuthorId>, ?ct: CancellationToken) = 
                this.SearchBooksByAuthorsAsync(authors, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByAuthorsAndYearAsync(authors: List<AuthorId>, year: YearSearch, ?ct: CancellationToken) = 
                this.SearchBooksByAuthorsAndYearAsync(authors, year, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndAuthorsAsync(title: Title, authors: List<AuthorId>, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndAuthorsAsync(title, authors, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndAuthorsAndYearAsync(title: Title, authors: List<AuthorId>, year: YearSearch, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndAuthorsAndYearAsync(title, authors, year, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByAuthorsAndCategoriesAsync(authors: List<AuthorId>, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByAuthorsAndCategoriesAsync(authors, categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndAuthorsAndCategoriesAsync(title: Title, authors: List<AuthorId>, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndAuthorsAndCategoriesAsync(title, authors, categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByAuthorsAndYearAndCategoriesAsync(authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByAuthorsAndYearAndCategoriesAsync(authors, year, categories, ct |> Option.defaultValue CancellationToken.None)
            member this.SearchByTitleAndAuthorsAndYearAndCategoriesAsync(title: Title, authors: List<AuthorId>, year: YearSearch, categories: List<Category>, ?ct: CancellationToken) = 
                this.SearchBooksByTitleAndAuthorsAndYearAndCategoriesAsync(title, authors, year, categories, ct |> Option.defaultValue CancellationToken.None)
