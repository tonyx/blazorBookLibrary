
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

type ReservationService
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
        ReservationService (
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
        ReservationService (
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
        ReservationService(connectionString)

        member this.AddReservationAsync (reservation: Reservation, dateTime: System.DateTime, ?ct: CancellationToken)= 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None

                    let! book =
                        bookViewerAsync (Some ct) reservation.BookId.Value
                        |> TaskResult.map snd

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

    member this.GetReservationAsync (id: ReservationId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result = 
                    reservationViewerAsync (Some ct) id.Value
                    |> TaskResult.map snd
                return result
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
        member this.AddReservationAsync (reservation: Reservation, ?ct: CancellationToken)= 
            let ct = Option.defaultValue CancellationToken.None ct
            this.AddReservationAsync (reservation, DateTime.UtcNow, ct)
        member this.GetReservationAsync (id: ReservationId, ?ct: CancellationToken) = 
            let ct = Option.defaultValue CancellationToken.None ct
            this.GetReservationAsync (id, ct)
        member this.RemoveReservationAsync (reservationId: ReservationId, ?ct:CancellationToken)= 
            let ct = Option.defaultValue CancellationToken.None ct
            this.RemoveReservationAsync (reservationId, DateTime.UtcNow, ct)            
        member this.GetReservationsAsync(ids: List<ReservationId>, ?ct: CancellationToken)= 
            let ct = Option.defaultValue CancellationToken.None ct
            this.GetReservationsAsync (ids, ct)
            