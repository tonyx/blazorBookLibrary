
namespace BookLibrary.Server.CleanServices
open Microsoft.Extensions.Hosting
open System
open Microsoft.Extensions.DependencyInjection
open System.Threading
open BookLibrary.CleanServices

type ScheduledWorker(scopeFactory: IServiceScopeFactory) =
    inherit BackgroundService()
    let interval = TimeSpan.FromMinutes(10L)

    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        task {
            use timer = new PeriodicTimer(interval)
            while! (timer.WaitForNextTickAsync stoppingToken)
                do
                    use scope = scopeFactory.CreateScope()
                    let mailResenderService = scope.ServiceProvider.GetRequiredService<IMailResenderService>()
                    let! _ = mailResenderService.ReSendPendingItemsAsync stoppingToken
                    ()
        }
