
namespace BookLibrary.Server.CleanServices
open Microsoft.Extensions.Hosting
open System
open Microsoft.Extensions.DependencyInjection
open System.Threading
open BookLibrary.Shared.Services
open BookLibrary.Shared

type ExpiredReservationsRemovalScheduler(scopeFactory: IServiceScopeFactory) =
    inherit BackgroundService()

    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        task {
            use timer = new PeriodicTimer(TimeSpan.FromMinutes(Commons.expiredReservationCleanupTimeBoxHours * 60 |> int64))
            while! (timer.WaitForNextTickAsync stoppingToken)
                do
                    use scope = scopeFactory.CreateScope()
                    let reservationService = scope.ServiceProvider.GetRequiredService<IReservationService>()
                    let! _ = reservationService.RemoveExpiredReservationsAsync() |> Async.AwaitTask
                    ()
        }