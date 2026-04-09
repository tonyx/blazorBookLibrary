
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
open BookLibrary.Details.Details
open System.Globalization
open blazorBookLibrary.Shared.Infrastructure.Services

type ReservationService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        userViewerAsync: AggregateViewerAsync2<User>,
        usersService: IUserService,
        mailNotificator: IMailNotificator,
        maxReservations: int,
        fromEmail: string,
        fromName: string
    ) =
    new (eventStore: IEventStore<string>, userService: IUserService, mailNotificator: IMailNotificator, configuration: IConfiguration)
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let maxReservations = configuration.GetValue<int>("BooksLibrary:MaxReservationsPerUser", 3)
        let fromEmail = configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com")
        let fromName = configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library")
        ReservationService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            userService,
            mailNotificator,
            maxReservations,
            fromEmail,
            fromName
        )
    new (configuration: IConfiguration, userService: IUserService, mailNotificator: IMailNotificator) 
        =
        let connectionString = configuration.GetConnectionString("BookLibraryDbConnection")
        let eventStore = PgStorage.PgEventStore connectionString
        ReservationService(eventStore, userService, mailNotificator, configuration) 

        member this.AddReservationAsync (reservation: Reservation, dateTime: System.DateTime, shortLang: ShortLang, ?ct: CancellationToken)= 

            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None

                    let! book =
                        bookViewerAsync (Some ct) reservation.BookId.Value
                        |> TaskResult.map snd

                    let! user =
                        userViewerAsync (Some ct) reservation.UserId.Value
                        |> TaskResult.map snd

                    let! userHasEnoughReservations = 
                        user.Reservations.Length < maxReservations
                        |> Result.ofBool "Already reached maximum number of reservations"

                    do!
                        reservation.TimeSlot.IsFutureOf(dateTime)
                        |> Result.ofBool "Reservation time slot must be in the future"

                    let! alreadyExistingReservations =
                        this.GetReservationsAsync book.CurrentReservations

                    let! noOverlaps =
                        alreadyExistingReservations
                        |> List.forall (fun r -> not (r.TimeSlot.Overlaps(reservation.TimeSlot)))
                        |> Result.ofBool "Reservation overlaps with existing reservation"

                    let addReservationToBookCommand = 
                        BookCommand.AddReservation (reservation.ReservationId, dateTime)

                    let addReservationToUserCommand = 
                        UserCommand.AddReservation reservation.ReservationId

                    let! userDetails = 
                        usersService.GetUserDetailsAsync (user.UserId, ct)

                    let templatePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Templates", shortLang.Value, "ReservationNotification.txt")
                    let! emailTextRetrieved = 
                        task {
                            try
                                let! content = System.IO.File.ReadAllTextAsync(templatePath)
                                return Ok content
                            with ex ->
                                return Error (sprintf "Error reading template %s: %s" templatePath ex.Message)
                        }

                    let! result =
                        runInitAndTwoAggregateCommandsMd<Book, BookEvent, User, UserEvent, string, Reservation>
                            book.Id
                            user.Id
                            eventStore
                            messageSenders
                            reservation
                            ""
                            addReservationToBookCommand
                            addReservationToUserCommand

                    let emailBody = emailTextRetrieved.Replace("{bookTitle}", book.Title.Value)
                    
                    do! 
                        task {
                            do! mailNotificator.SendEmailAsync(
                                    fromEmail,
                                    fromName,
                                    userDetails.ApplicationUser.Email,
                                    "Book Reservation Confirmation",
                                    emailBody
                                )
                            return Ok ()
                        }

                    return result
                }
        member this.AddReservationAsync (reservation: Reservation, dateTime: DateTime, ?ct: CancellationToken) =
            this.AddReservationAsync (reservation, dateTime, ShortLang.New "en", ?ct = ct)

    member this.GetReservationAsync (id: ReservationId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result = 
                    reservationViewerAsync (Some ct) id.Value
                    |> TaskResult.map snd
                return result
            }
    member this.GetRefreshableReservationDetailsAsync (id: ReservationId, ?ct: CancellationToken) = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let refresher = 
                    fun () ->
                        result
                            {
                                let ct = ct |> Option.defaultValue CancellationToken.None
                                let! reservation = 
                                    reservationViewerAsync (ct |> Some) id.Value |> TaskResult.map snd
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let! book = 
                                    bookViewerAsync (ct |> Some) reservation.BookId.Value |> TaskResult.map snd
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let! userDetails = 
                                    usersService.GetUserDetailsAsync (reservation.UserId, ct)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                return 
                                    {
                                        Reservation = reservation
                                        Book = book
                                        UserDetails = userDetails
                                    }
                            }
                result {
                    let! reservationDetails = refresher()
                    return 
                        {
                            ReservationDetails = reservationDetails    
                            Refresher = refresher
                        } :> Refreshable<RefreshableReservationDetails>
                        ,
                        [id.Value ;
                        reservationDetails.Reservation.BookId.Value ;
                        reservationDetails.Book.BookId.Value]
                    }
        let key = DetailsCacheKey.OfType typeof<RefreshableReservationDetails> id.Value
        task
            {
                return StateView.getRefreshableDetailsAsync<RefreshableReservationDetails> (fun ct -> detailsBuilder ct) key ct
            }

    member this.RemoveReservationAsync (reservationId: ReservationId, dateTime: System.DateTime, ?ct:CancellationToken)= 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! reservation = 
                    this.GetReservationAsync(reservationId, ct)
                let! book =
                    bookViewerAsync (Some ct) reservation.BookId.Value
                    |> TaskResult.map snd
                let! user =     
                    userViewerAsync (Some ct) reservation.UserId.Value
                    |> TaskResult.map snd

                let removeReservationFromBook: AggregateCommand<Book, BookEvent> =
                    BookCommand.RemoveReservation (reservation.ReservationId, dateTime)

                let removeReservationFromUser: AggregateCommand<User, UserEvent> =
                    UserCommand.RemoveReservation reservation.ReservationId

                let! result =
                    runDeleteAndTwoAggregateCommandsMd<Reservation, ReservationEvent, Book, BookEvent, User, UserEvent, string>
                        eventStore
                        messageSenders
                        ""
                        reservationId.Value
                        book.Id
                        user.Id
                        removeReservationFromBook
                        removeReservationFromUser
                        (fun _ -> true)
                return result
            }

    member this.GetReservationsAsync (ids: List<ReservationId>, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result = 
                    ids
                    |> List.traverseTaskResultM (fun id -> this.GetReservationAsync (id, ct))
                return result
            }

    interface IReservationService with
        member this.AddReservationAsync (reservation: Reservation, shortLang: ShortLang, ?ct: CancellationToken)= 
            let ct = defaultArg ct CancellationToken.None
            this.AddReservationAsync (reservation, DateTime.UtcNow, shortLang, ct)
        member this.GetReservationAsync (id: ReservationId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetReservationAsync (id, ct)
        member this.GetReservationDetailsAsync (id: ReservationId, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableDetails = this.GetRefreshableReservationDetailsAsync (id, ct)
                    return refreshableDetails.ReservationDetails
                }
        member this.RemoveReservationAsync (reservationId: ReservationId, ?ct:CancellationToken)= 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveReservationAsync (reservationId, DateTime.UtcNow, ct)            
        member this.GetReservationsAsync(ids: List<ReservationId>, ?ct: CancellationToken)= 
            let ct = defaultArg ct CancellationToken.None
            this.GetReservationsAsync (ids, ct)
            