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
                                let! futureReservations = 
                                    book.CurrentReservations
                                    |> List.traverseTaskResultM (fun reservationId -> reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                    
                                return 
                                    { 
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
                            (bookDetails.FutureReservations |> List.map _.ReservationId.Value)
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

            member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken): TaskResult<BookDetails, string> = 
                taskResult {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableBookDetails =
                        this.GetRefreshableBookDetailsAsync(bookId, ct)
                    return refreshableBookDetails.BookDetails
                }

        interface IBookService with                
            member this.AddAuthorToBookAsync(authorId: AuthorId, bookId: BookId, ?ct: CancellationToken) =
                let dateTime = System.DateTime.UtcNow
                this.AddAuthorToBookAsync(authorId, bookId, dateTime, ct |> Option.defaultValue CancellationToken.None)
            member this.AddBookAsync(book: Book, ?ct: CancellationToken ) =
                this.AddBookAsync(book, ct |> Option.defaultValue CancellationToken.None)
            member this.GetBookAsync(id: BookId, ?ct: CancellationToken) =
                this.GetBookAsync(id, ct |> Option.defaultValue CancellationToken.None)
            member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken) = 
                this.GetBookDetailsAsync(bookId, ct |> Option.defaultValue CancellationToken.None)
            member this.RemoveAuthorFromBookAsync(authorId: AuthorId, bookId: BookId, ?ct: CancellationToken) = 
                let dateTime = System.DateTime.UtcNow
                this.RemoveAuthorFromBookAsync(authorId, bookId, dateTime, ct |> Option.defaultValue CancellationToken.None)
