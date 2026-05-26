namespace BookLibrary.CleanServices

open System.Threading
open System
open Sharpino
open Sharpino.Storage

open BookLibrary.Domain
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Configuration
open Microsoft.Extensions.Logging

module CleanUpServices =
    open BookLibrary.Shared.Commons

    type CleanUpService(configuration: IConfiguration, logger: ILogger<CleanUpService>) =
        let bookDbConnectionString =
            configuration.GetConnectionString "BookLibraryDbConnection"
        // todo would be bettere to inject the event store
        let bookEventStore: IEventStore<string> =
            PgStorage.PgEventStore bookDbConnectionString

        member this.ReSnapshotOnStartup() : TaskResult<unit, string> =
            taskResult {
                let snapshotsAllBooks =
                    configuration.GetValue<bool>("SnapshotsAllBooksOnStartup", false)

                let snapshotsAllAuthors =
                    configuration.GetValue<bool>("SnapshotsAllAuthorsOnStartup", false)

                let snapshotsAllEditors =
                    configuration.GetValue<bool>("SnapshotsAllEditorsOnStartup", false)

                let snapshotsAllDistributionPoints =
                    configuration.GetValue<bool>("SnapshotsAllDistributionPointsOnStartup", false)

                let snapshotsAllLoans =
                    configuration.GetValue<bool>("SnapshotsAllLoansOnStartup", false)

                let snapshotsAllReservations =
                    configuration.GetValue<bool>("SnapshotsAllReservationsOnStartup", false)

                let snapshotsAllUsers =
                    configuration.GetValue<bool>("SnapshotsAllUsersOnStartup", false)

                let snapthosAllTenants =
                    configuration.GetValue<bool>("SnapshotsAllTenantsOnStartup", false)

                let snapshotsAllReviewsOnStartup =
                    configuration.GetValue<bool>("SnapshotsAllReviewsOnStartup", false)

                if snapshotsAllBooks then
                    do! this.ReSnapshotAllBooks()

                if snapshotsAllAuthors then
                    do! this.ReSnapshotAllAuthors()

                if snapshotsAllEditors then
                    do! this.ReSnapshotAllEditors()

                if snapshotsAllDistributionPoints then
                    do! this.ReSnapshotAllDistributionPoints()

                if snapshotsAllLoans then
                    do! this.ReSnapshotAllLoans()

                if snapshotsAllReservations then
                    do! this.ReSnapshotAllReservations()

                if snapshotsAllUsers then
                    do! this.ReSnapshotAllUsers()

                if snapthosAllTenants then
                    do! this.ResnapshotAllTenants()

                if snapshotsAllReviewsOnStartup then
                    do! this.ReSnapshotAllReviews()

                return ()
            }

        member this.ResnapshotAllTenants() =
            logger.LogInformation("ResnapshotAllTenants")

            let upcaster (data: string) =
                result {
                    let! deserialized = Tenant.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let! result =
                    bookEventStore.BulkSnapshotsUpcast(Tenant.Version, Tenant.StorageName, upcaster, cts.Token)

                logger.LogInformation("ResnapshotAllTenants result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllBooks() =
            logger.LogInformation("ReSnapshotAllBooks")

            let upcaster (data: string) =
                result {
                    let! deserialized = Book.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(Book.Version, Book.StorageName, upcaster, cts.Token)

                logger.LogInformation("ReSnapshotAllBooks result: {0}", result)
                return ()
            }

        member this.ReSnapshotAllAuthors() =
            logger.LogInformation("ReSnapshotAllAuthors")

            let upcaster (data: string) =
                result {
                    let! deserialized = Author.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(Author.Version, Author.StorageName, upcaster, cts.Token)

                logger.LogInformation("ReSnapshotAllAuthors result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllEditors() =
            logger.LogInformation("ReSnapshotAllEditors")

            let upcaster (data: string) =
                result {
                    let! deserialized = Editor.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(Editor.Version, Editor.StorageName, upcaster, cts.Token)

                logger.LogInformation("ReSnapshotAllEditors result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllLoans() =
            logger.LogInformation("ReSnapshotAllLoans")

            let upcaster (data: string) =
                result {
                    let! deserialized = Loan.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(Loan.Version, Loan.StorageName, upcaster, cts.Token)

                logger.LogInformation("ReSnapshotAllLoans result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllReservations() =
            logger.LogInformation("ReSnapshotAllReservations")

            let upcaster (data: string) =
                result {
                    let! deserialized = Reservation.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(
                        Reservation.Version,
                        Reservation.StorageName,
                        upcaster,
                        cts.Token
                    )

                logger.LogInformation("ReSnapshotAllReservations result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllUsers() =
            logger.LogInformation("ReSnapshotAllUsers")

            let upcaster (data: string) =
                result {
                    let! deserialized = User.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(User.Version, User.StorageName, upcaster, cts.Token)

                logger.LogInformation("ReSnapshotAllUsers result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllReviews() =
            logger.LogInformation("ReSnapshotAllReviews")

            let upcaster (data: string) =
                result {
                    let! deserialized = Review.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(Review.Version, Review.StorageName, upcaster, cts.Token)

                logger.LogInformation("ReSnapshotAllReviews result: {0}", result)

                return ()
            }

        member this.ReSnapshotAllDistributionPoints() =
            logger.LogInformation("ReSnapshotAllDistributionPoints")

            let upcaster (data: string) =
                result {
                    let! deserialized = DistributionPoint.Deserialize data
                    return (deserialized.Serialize)
                }

            taskResult {
                use cts = new CancellationTokenSource(delay = TimeSpan.FromMinutes(10.0))

                let result =
                    bookEventStore.BulkSnapshotsUpcast(
                        DistributionPoint.Version,
                        DistributionPoint.StorageName,
                        upcaster,
                        cts.Token
                    )

                logger.LogInformation("ReSnapshotAllDistributionPoints result: {0}", result)

                return ()
            }
