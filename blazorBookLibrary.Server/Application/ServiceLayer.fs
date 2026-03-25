
namespace BookLibrary.Application
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
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks

module ServiceLayer =
    open BookLibrary.Details.Details
    type BookLibraryService
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
            BookLibraryService (
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
            BookLibraryService (
                eventStore,
                messageSenders,
                bookViewerAsync,
                authorViewerAsync,
                editorViewerAsync,
                reservationViewerAsync,
                loanViewerAsync
            )    
        new (configuration: Microsoft.Extensions.Configuration.IConfiguration) 
            =
            let connectionString = configuration.Item("ConnectionStrings::BookLibraryDbConnection")
            BookLibraryService(connectionString)

            member this.AddBookAsync (book: Book, ?ct: CancellationToken) =
                let ct = defaultArg ct CancellationToken.None
                taskResult
                    {
                        let! authors = 
                            book.Authors
                            |> List.traverseTaskResultM (fun authorId -> this.GetAuthorAsync authorId)

                        let authorAddBooks: List<AggregateCommand<Author, AuthorEvent>> =
                            authors
                            |>> (fun _ -> AuthorCommand.AddBook book.BookId)

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
                            this.GetBookAsync bookId

                        let! author =
                            this.GetAuthorAsync authorId

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
                            this.GetBookAsync (bookId, ct)
                        let! author = 
                            this.GetAuthorAsync authorId
                        printf "books of author %A\n" author.Books
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

            member this.AddAuthorAsync (author: Author, ?ct: CancellationToken) =
                let ct = defaultArg ct CancellationToken.None
                taskResult
                    {
                        return!
                            runInitAsync<Author, AuthorEvent, string>
                            eventStore
                            messageSenders
                            author
                            (Some ct)
                    }
            
            member this.AddEditorAsync (editor: Editor, ?ct: CancellationToken) =
                let ct = defaultArg ct CancellationToken.None
                taskResult
                    {
                        return!
                            runInitAsync<Editor, EditorEvent, string>
                            eventStore
                            messageSenders
                            editor
                            (Some ct)
                    }

            member this.GetAuthorAsync (id: AuthorId): Task<Result<Author, string>> = 
                taskResult
                    {
                        let! result = 
                            authorViewerAsync None id.Value 
                        return result |> snd
                    }

            member this.GetBookAsync (id: BookId, ?ct: CancellationToken): Task<Result<Book, string>> = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! result = 
                            bookViewerAsync (Some ct) id.Value 
                        return result |> snd
                    }

            member this.AddReservationAsync (reservation: Reservation, dateTime: System.DateTime, ?ct: CancellationToken)= 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None

                        let! book =
                            this.GetBookAsync(reservation.BookId, ct)

                        do!
                            reservation.TimeSlot.IsFutureOf(dateTime)
                            |> Result.ofBool "Reservation time slot must be in the future"

                        let! alreadyExistingReservations =
                            this.GetReservationsAsync book.CurrentReservations

                        let! noOverlaps =
                            alreadyExistingReservations
                            |> List.forall (fun r -> not (r.TimeSlot.Overlaps(reservation.TimeSlot)))
                            |> Result.ofBool "Reservation overlaps with existing reservation"

                        let addReservationCommand = 
                            BookCommand.AddReservation (reservation.ReservationId, dateTime)

                        let! result =
                            runInitAndNAggregateCommandsMdAsync<Book, BookEvent, Reservation, string>
                                [book.Id]
                                eventStore
                                messageSenders
                                reservation
                                ""
                                [addReservationCommand]
                                (Some ct)
                        return result
                    }

            member this.RemoveReservationAsync (reservationId: ReservationId, dateTime: System.DateTime, ?ct:CancellationToken)= 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! reservation = 
                            this.GetReservationAsync(reservationId, ct)
                        let! book =
                            this.GetBookAsync(reservation.BookId, ct)
                        let removeReservationFromBook: AggregateCommand<Book, BookEvent> =
                            BookCommand.RemoveReservation (reservation.ReservationId, dateTime)

                        let! result =
                            runDeleteAndAggregateCommandMd<Reservation, ReservationEvent, Book, BookEvent, string>
                                eventStore
                                messageSenders
                                ""
                                reservationId.Value
                                book.Id
                                removeReservationFromBook
                                (fun _ -> true)
                        return result
                    }

            member this.AddLoanAsync (loan: Loan, dateTime: System.DateTime, ?ct: CancellationToken)= 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! book = 
                            bookViewerAsync (Some ct) loan.BookId.Value
                        let book = book |> snd
                        let setCurrentLoanCommand = 
                            BookCommand.SetCurrentLoan (loan.LoanId, dateTime)

                        let! result = 
                            runInitAndNAggregateCommandsMdAsync<Book, BookEvent, Loan, string>
                                [book.Id]
                                eventStore
                                messageSenders
                                loan
                                ""
                                [setCurrentLoanCommand]
                                (Some ct)
                        return result
                    }

            member this.ReleaseLoanAsync (loanId: LoanId, dateTime: System.DateTime, ?ct: CancellationToken)= 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! loan = 
                            loanViewerAsync (Some ct) loanId.Value
                        let loan = loan |> snd
                        let! book = 
                            bookViewerAsync (Some ct) loan.BookId.Value
                        let book = book |> snd
                        let releaseLoanCommand = 
                            BookCommand.ReleaseLoan (loanId, dateTime)

                        let! result = 
                            runInitAndNAggregateCommandsMdAsync<Book, BookEvent, Loan, string>
                                [book.Id]
                                eventStore
                                messageSenders
                                loan
                                ""
                                [releaseLoanCommand]
                                (Some ct)
                        return result
                    }

            member private this.GetRefreshableBookDetailsAsync(bookId: BookId, ?ct:CancellationToken) =
                let detailsBuilder =
                    fun (ct: Option<CancellationToken>) ->
                        let refresher =
                            fun () ->
                                result {
                                    let ct = ct |> Option.defaultValue CancellationToken.None
                                    let! book = 
                                        this.GetBookAsync (bookId, ct)
                                        |> Async.AwaitTask
                                        |> Async.RunSynchronously
                                    let! currentLoan = 
                                        match book.CurrentLoan with
                                        | Some loanId -> 
                                            let loan = 
                                                this.GetLoanAsync loanId
                                                |> Async.AwaitTask
                                                |> Async.RunSynchronously
                                            match loan with
                                            | Ok loan -> loan |> Some |> Ok
                                            | Error x ->  Error x
                                        | None -> 
                                            None |> Ok
                                    let! futureReservations = 
                                        this.GetReservationsAsync book.CurrentReservations
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

            member this.GetBookDetailsAsync(bookId: BookId, ?ct: CancellationToken): TaskResult<BookDetails, string> = 
                taskResult {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableBookDetails =
                        this.GetRefreshableBookDetailsAsync(bookId, ct)
                    return refreshableBookDetails.BookDetails
                }

            member this.GetLoanAsync (id: LoanId, ?ct: CancellationToken): TaskResult<Loan, string> = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! result =
                            loanViewerAsync (Some ct) id.Value
                        return result |> snd
                    }

            member this.GetReservationAsync (id: ReservationId, ?ct: CancellationToken): TaskResult<Reservation, string> = 
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        let! result =
                            reservationViewerAsync (Some ct) id.Value
                        return result |> snd
                    }

            member this.GetReservationsAsync (ids: List<ReservationId>, ?ct: CancellationToken): TaskResult<List<Reservation>, string> =
                taskResult
                    {
                        let ct = defaultArg ct CancellationToken.None
                        return! 
                            ids
                            |> List.traverseTaskResultM (fun id -> this.GetReservationAsync (id, ct))
                    }
