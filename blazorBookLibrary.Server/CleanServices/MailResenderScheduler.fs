
namespace BookLibrary.Server.CleanServices
open Microsoft.Extensions.Hosting
open System
open Microsoft.Extensions.DependencyInjection
open System.Threading
open BookLibrary.CleanServices
open BookLibrary.Shared
open Microsoft.Extensions.Configuration

type MailResenderScheduler(scopeFactory: IServiceScopeFactory, configuration: IConfiguration) =
    inherit BackgroundService()

    override this.ExecuteAsync(stoppingToken: CancellationToken) =
        let resendEmailTimeBoxMinMinutes = configuration.GetValue<int>("BooksLibrary:ResendEmailTimeBoxMinutes", 10)
        task {
            use timer = new PeriodicTimer(TimeSpan.FromMinutes(resendEmailTimeBoxMinMinutes |> int64))
            while! (timer.WaitForNextTickAsync stoppingToken)
                do
                    use scope = scopeFactory.CreateScope()
                    let mailResenderService = scope.ServiceProvider.GetRequiredService<IMailResenderService>()
                    let! _ = mailResenderService.ReSendPendingItemsAsync stoppingToken
                    ()
        }
