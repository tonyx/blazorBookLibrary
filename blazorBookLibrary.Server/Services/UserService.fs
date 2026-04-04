
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
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open Microsoft.Extensions.DependencyInjection

type UserService 
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        userViewerAsync: AggregateViewerAsync2<User>,
        scopeFactory: IServiceScopeFactory

        // userManager: UserManager<ApplicationUser>
    ) =
    new (eventStore: IEventStore<string>, scopeFactory: IServiceScopeFactory) 
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        UserService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            scopeFactory
        )    

    new (configuration: IConfiguration, scopeFactory: IServiceScopeFactory) 
        =
        let connectionString = configuration.GetConnectionString("BookLibraryDbConnection")
        let eventStore = PgStorage.PgEventStore connectionString
        UserService(eventStore, scopeFactory)

    member this.CreateUserAsync (user: User, ?ct: CancellationToken) : Task<Result<unit, string>> =
        taskResult 
            {
                let result =
                    runInitAsync<User, UserEvent, string>
                        eventStore
                        messageSenders
                        user
                        ct
                return! result
            }

    member this.GetUserAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
        taskResult 
            {
                let ct = defaultArg ct CancellationToken.None
                let! user = userViewerAsync (Some ct) userId.Value |> TaskResult.map snd
                return user
            }

    member private 
        this.GetRefreshableUserDetailsAsync(userId: UserId, ?ct:CancellationToken): TaskResult<RefreshableUserDetails, string> =
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let refresher =
                        fun () ->
                            use scope = scopeFactory.CreateScope()
                            let userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                            result {
                                let ct = ct |> Option.defaultValue CancellationToken.None
                                let! user = 
                                    userViewerAsync (ct |> Some) userId.Value 
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                    |> Result.map snd
                                let! futureReservations = 
                                    user.Reservations
                                    |> List.traverseTaskResultM (fun reservationId -> reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let! currentLoans = 
                                    user.CurrentLoans
                                    |> List.traverseTaskResultM (fun loanId -> loanViewerAsync (Some ct) loanId.Value |> TaskResult.map snd)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let appUser = 
                                    try
                                        let appUser = 
                                            userManager.FindByIdAsync(userId.Value.ToString()) |> Async.AwaitTask |> Async.RunSynchronously
                                        if appUser = null then
                                            ApplicationUser(UserName = "unknown", CodiceFiscale = "unknown")
                                        else
                                            appUser
                                    with
                                        | ex -> 
                                            printfn "Error getting user: %s" ex.Message
                                            ApplicationUser(UserName = "unknown", CodiceFiscale = "unknown")
                                    
                                return 
                                    { 
                                        User = user
                                        ApplicationUser = appUser
                                        FutureReservations = futureReservations
                                        CurrentLoans = currentLoans
                                    } 
                            }
                    result {
                        let! userDetails = refresher ()
                        return 
                            { 
                                UserDetails = userDetails
                                Refresher = refresher
                            } :> Refreshable<RefreshableUserDetails>
                            ,
                            userId.Value :: 
                            (userDetails.CurrentLoans |> List.map (fun x -> x.LoanId.Value)) @ 
                            (userDetails.FutureReservations |> List.map (fun x -> x.ReservationId.Value))
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableUserDetails> userId.Value
            task {
                return StateView.getRefreshableDetailsAsync<RefreshableUserDetails> (fun ct -> detailsBuilder ct) key ct
            }

    member this.GetUserDetailsAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<UserDetails, string>> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! refreshableUserDetails =
                this.GetRefreshableUserDetailsAsync(userId, ct)
            return refreshableUserDetails.UserDetails
        }

    interface IUserService with
        member this.CreateUserAsync (user: User, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.CreateUserAsync(user, ct)
        member this.GetUserAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUserAsync(userId, ct)
        member this.GetUserDetailsAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<UserDetails, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUserDetailsAsync(userId, ct)
                 