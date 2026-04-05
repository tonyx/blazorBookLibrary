
namespace BookLibrary.CleanServices
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
open Sharpino.StateView

open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks

open BookLibrary.Shared.Details
open BookLibrary.Details.Details

open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging

module CleanUpServices =

    type CleanUpService
        (
            configuration: IConfiguration,
            logger: ILogger<CleanUpService>
        ) =
        let bookDbConnectionString = configuration.GetConnectionString "BookLibraryDbConnection"
        // todo would be bettere to inject the event store 
        let bookEventStore: IEventStore<string> = PgStorage.PgEventStore bookDbConnectionString

        member this.ReSnapshotOnStartup (): TaskResult<unit, string> = 
            taskResult
                {
                    let snapshotsAllBooks = configuration.GetValue<bool>("SnapshotsAllBooksOnStartup", false)
                    let snapshotsAllAuthors = configuration.GetValue<bool>("SnapshotsAllAuthorsOnStartup", false)
                    let snapshotsAllEditors = configuration.GetValue<bool>("SnapshotsAllEditorsOnStartup", false)
                    let snapshotsAllLoans = configuration.GetValue<bool>("SnapshotsAllLoansOnStartup", false)
                    let snapshotsAllReservations = configuration.GetValue<bool>("SnapshotsAllReservationsOnStartup", false)
                    let snapshotsAllUsers = configuration.GetValue<bool>("SnapshotsAllUsersOnStartup", false)
                
                    if snapshotsAllBooks then
                        do! this.ReSnapshotAllBooks()
                    if snapshotsAllAuthors then
                        do! this.ReSnapshotAllAuthors()
                    if snapshotsAllEditors then
                        do! this.ReSnapshotAllEditors()
                    if snapshotsAllLoans then
                        do! this.ReSnapshotAllLoans()
                    if snapshotsAllReservations then
                        do! this.ReSnapshotAllReservations()
                    if snapshotsAllUsers then
                        do! this.ReSnapshotAllUsers()
                    return ()
                }

        member this.ReSnapshotAllBooks() = 
            logger.LogInformation("ReSnapshotAllBooks") 
            taskResult
                {
                    use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))
                    let! ids = 
                        bookEventStore.GetUndeletedAggregateIdsAsync (Book.Version, Book.StorageName, cts.Token)
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    let! result =
                        ids 
                        |> List.traverseResultM (fun id -> 
                                CommandHandler.mkAggregateSnapshot<Book, BookEvent, string> bookEventStore id
                            )
                    return ()
                }

        member this.ReSnapshotAllAuthors() = 
            logger.LogInformation("ReSnapshotAllAuthors") 
            taskResult
                {
                    use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))
                    let! ids = 
                        bookEventStore.GetUndeletedAggregateIdsAsync (Author.Version, Author.StorageName, cts.Token)
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    let! result =
                        ids 
                        |> List.traverseResultM (fun id -> 
                                CommandHandler.mkAggregateSnapshot<Author, AuthorEvent, string> bookEventStore id
                            )
                    return ()
                }
        member this.ReSnapshotAllEditors() = 
            logger.LogInformation("ReSnapshotAllEditors") 
            taskResult
                {
                    use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))
                    let! ids = 
                        bookEventStore.GetUndeletedAggregateIdsAsync (Editor.Version, Editor.StorageName, cts.Token)
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    let! result =
                        ids 
                        |> List.traverseResultM (fun id -> 
                                CommandHandler.mkAggregateSnapshot<Editor, EditorEvent, string> bookEventStore id
                            )
                    return ()
                }
        member this.ReSnapshotAllLoans() = 
            logger.LogInformation("ReSnapshotAllLoans") 
            taskResult
                {
                    use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))
                    let! ids = 
                        bookEventStore.GetUndeletedAggregateIdsAsync (Loan.Version, Loan.StorageName, cts.Token)
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    let! result =
                        ids 
                        |> List.traverseResultM (fun id -> 
                                CommandHandler.mkAggregateSnapshot<Loan, LoanEvent, string> bookEventStore id
                            )
                    return ()
                }
        member this.ReSnapshotAllReservations() = 
            logger.LogInformation("ReSnapshotAllReservations") 
            taskResult
                {
                    use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))
                    let! ids = 
                        bookEventStore.GetUndeletedAggregateIdsAsync (Reservation.Version, Reservation.StorageName, cts.Token)
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    let! result =
                        ids 
                        |> List.traverseResultM (fun id -> 
                                CommandHandler.mkAggregateSnapshot<Reservation, ReservationEvent, string> bookEventStore id
                            )
                    return ()
                }
        member this.ReSnapshotAllUsers() = 
            logger.LogInformation("ReSnapshotAllUsers") 
            taskResult
                {
                    use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))
                    let! ids = 
                        bookEventStore.GetUndeletedAggregateIdsAsync (User.Version, User.StorageName, cts.Token)
                        |> Async.AwaitTask
                        |> Async.RunSynchronously

                    let! result =
                        ids 
                        |> List.traverseResultM (fun id -> 
                                CommandHandler.mkAggregateSnapshot<User, UserEvent, string> bookEventStore id
                            )
                    return ()
                }