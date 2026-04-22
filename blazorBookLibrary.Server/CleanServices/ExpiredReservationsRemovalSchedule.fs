
namespace BookLibrary.Server.CleanServices
open Microsoft.Extensions.Hosting
open System
open Microsoft.Extensions.DependencyInjection
open System.Threading
open BookLibrary.Shared.Services
open BookLibrary.Shared
open Microsoft.Extensions.Configuration

type ExpiredReservationsRemovalScheduler(scopeFactory: IServiceScopeFactory, configuration: IConfiguration) =
    inherit BackgroundService()

    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        let expiredReservationCleanupTimeBox = configuration.GetValue<int>("BooksLibrary:ExpiredReservationCleanupTimeBoxHours", 1) * 60 |> int64

        task {
            // use timer = new PeriodicTimer(TimeSpan.FromMinutes(Commons.expiredReservationCleanupTimeBoxHours * 60 |> int64))
            use timer = new PeriodicTimer(TimeSpan.FromMinutes(expiredReservationCleanupTimeBox))
            while! (timer.WaitForNextTickAsync stoppingToken)
                do
                    use scope = scopeFactory.CreateScope()
                    let reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>()
                    let! _ = reservationService.RemoveExpiredReservationsAsync()
                    ()
        }