
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
open BookLibrary.Services.UserMapping

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
        reviewsViewerAsync: AggregateViewerAsync2<Review>,
        reviewService: IReviewService,
        scopeFactory: IServiceScopeFactory)
    =
    new (eventStore: IEventStore<string>, scopeFactory: IServiceScopeFactory, reviewService: IReviewService) 
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let reviewsViewerAsync = getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore
        UserService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            reviewsViewerAsync,
            reviewService,
            scopeFactory
        )    

    new (configuration: IConfiguration, scopeFactory: IServiceScopeFactory, secretsReader: BookLibrary.Utils.SecretsReader, reviewService: IReviewService)
        =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        UserService(eventStore, scopeFactory, reviewService)

    member this.MakeUserDetailsRefresherAsync(id: UserId, ?ct: CancellationToken) = 
        fun (ct: Option<CancellationToken>) -> 
            taskResult 
                {
                    let ct = ct |> Option.defaultValue CancellationToken.None
                    let! user = userViewerAsync (Some ct) id.Value |> TaskResult.map snd
                    let! futurereservations = 
                        user.Reservations 
                        |> List.traverseTaskResultM (fun reservationId -> reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd)
                    let! currentLoans =
                        user.CurrentLoans
                        |> List.traverseTaskResultM (fun loanId -> loanViewerAsync (Some ct) loanId.Value |> TaskResult.map snd)

                    let! reservedBooks =
                        futurereservations
                        |> List.traverseTaskResultM (fun reservation -> bookViewerAsync (Some ct) reservation.BookId.Value |> TaskResult.map snd)

                    let reservationsAndBooks =
                        List.zip futurereservations reservedBooks

                    let! loansedBooks =
                        currentLoans
                        |> List.traverseTaskResultM (fun loan -> bookViewerAsync (Some ct) loan.BookId.Value |> TaskResult.map snd)

                    let loansAndBooks =
                        List.zip currentLoans loansedBooks

                    let! booksAndReviews =
                        reviewService.GetReviewsOfUserAsync(id, ct)
                        
                    return 
                        {
                            User = user
                            AppUser = user.AppUserInfo
                            FutureReservations = reservationsAndBooks
                            CurrentLoans = loansAndBooks
                            BooksAndReviews = booksAndReviews
                        }
                }

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
                    let ct = ct |> Option.defaultValue CancellationToken.None
                    let refresher = this.MakeUserDetailsRefresherAsync(userId)

                    taskResult {
                        let! userDetails = refresher (Some ct) 
                        return 
                            { 
                                UserDetails = userDetails
                                Refresher = refresher 
                            } :> RefreshableAsync<RefreshableUserDetails>
                            ,
                            userId.Value :: 
                            (userDetails.CurrentLoans |> List.map (fun (x,_) -> x.LoanId.Value)) @ 
                            (userDetails.FutureReservations |> List.map (fun (x, _) -> x.ReservationId.Value)) @
                            (userDetails.FutureReservations |> List.map (fun (_, x) -> x.BookId.Value)) @
                            (userDetails.CurrentLoans |> List.map (fun (_, x) -> x.BookId.Value))
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableUserDetails> userId.Value
            StateView.getRefreshableDetailsTaskResultAsync<RefreshableUserDetails> (fun ct -> detailsBuilder ct) key ct

    member this.GetUserDetailsAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<UserDetails, string>> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! refreshableUserDetails =
                this.GetRefreshableUserDetailsAsync(userId, ct)
            return refreshableUserDetails.UserDetails
        }

    member private this.UpdateAppUserPropertyAsync (userId: UserId, updateAction: ApplicationUser -> unit, ?ct: CancellationToken) : Task<Result<unit, string>> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            use scope = scopeFactory.CreateScope()
            let userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
            let userIdStr = userId.Value.ToString()
            let! appUser = 
                task {
                    let! user = userManager.FindByIdAsync(userIdStr)
                    if user <> null then return Ok user else return Error (sprintf "User %s not found" userIdStr)
                }
            
            updateAction appUser
            let! updateResult = 
                task {
                    let! result = userManager.UpdateAsync(appUser)
                    if result.Succeeded then return Ok () else return Error (result.Errors |> Seq.map (fun e -> e.Description) |> String.concat ", ")
                }
            let! _ = DetailsCache.Instance.RefreshDependentDetailsAsync (userId.Value, Some ct)
            return updateResult
        }

    member private this.UpdateAppUserAndAggregateAsync (userId: UserId, updateAction: ApplicationUser -> unit, command: UserCommand, ?ct: CancellationToken) : Task<Result<unit, string>> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! res = this.UpdateAppUserPropertyAsync(userId, updateAction, ct)
            let! _ = 
                runAggregateCommandMdAsync<User, UserEvent, string>
                    userId.Value
                    eventStore
                    messageSenders
                    ""
                    command
                    (Some ct)
            
            return res
        }

    member this.SetFiscalCodeAsync (userId: UserId, fiscalCode: FiscalCode, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> u.CodiceFiscale <- fiscalCode.Value), SetCodiceFiscale fiscalCode, ?ct = ct)

    member this.SetNameAsync (userId: UserId, name: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> u.Nome <- name), SetNome name, ?ct = ct)

    member this.SetSurnameAsync (userId: UserId, surname: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> u.Cognome <- surname), SetCognome surname, ?ct = ct)

    member this.SetPhoneNumberAsync (userId: UserId, phoneNumber: PhoneNumber, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> u.PhoneNumber <- phoneNumber.Value), SetPhoneNumber phoneNumber, ?ct = ct)

    member this.SetIsPhysicallyIdentifiedAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> u.IsIdentifiedPhysically <- true), SetPhysicalIdentification, ?ct = ct)

    member this.UnSetIsPhysicallyIdentifiedAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> u.IsIdentifiedPhysically <- false), UnsetPhysicalIdentification, ?ct = ct)

    member this.GhostUserAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(userId, (fun u -> 
                    let ghostId = Guid.NewGuid().ToString().Substring(0, 8)
                    let ghostName = sprintf "ghosted_%s" ghostId
                    let ghostEmail = sprintf "ghosted_%s@example.com" ghostId
                    u.UserName <- ghostName
                    u.NormalizedUserName <- ghostName.ToUpper()
                    u.Email <- ghostEmail
                    u.NormalizedEmail <- ghostEmail.ToUpper()
                    u.Nome <- "Ghosted"
                    u.Cognome <- "Ghosted"
                    u.CodiceFiscale <- "GHOSTED"
                    u.PhoneNumber <- null
                    u.PasswordHash <- null
                    u.LockoutEnabled <- true
                    u.LockoutEnd <- Nullable<DateTimeOffset>(DateTimeOffset.MaxValue)
                ), GdprGhost, ?ct = ct)

    member private this.GetUser (userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! user = userViewerAsync (ct |> Some) userId.Value |> TaskResult.map snd
                return user
            }
    
    member this.SetAppUserInfoAsync (userId: UserId, appUserInfo: AppUserInfo, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let setAppUserInfoCommand = UserCommand.SetAppUserInfo appUserInfo
                let result =
                    runAggregateCommandMdAsync 
                        userId.Value
                        eventStore
                        messageSenders
                        ""
                        setAppUserInfoCommand
                        (ct |> Some)

                return! result
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
        member this.SetFiscalCodeAsync (userId: UserId, fiscalCode: FiscalCode, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetFiscalCodeAsync(userId, fiscalCode, ct)
        member this.SetNameAsync (userId: UserId, name: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetNameAsync(userId, name, ct)
        member this.SetSurnameAsync (userId: UserId, surname: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetSurnameAsync(userId, surname, ct)
        member this.SetPhoneNumberAsync (userId: UserId, phoneNumber: PhoneNumber, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetPhoneNumberAsync(userId, phoneNumber, ct)
        member this.SetIsPhysicallyIdentifiedAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetIsPhysicallyIdentifiedAsync(userId, ct)
        member this.UnSetIsPhysicallyIdentifiedAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.UnSetIsPhysicallyIdentifiedAsync(userId, ct)
        member this.GhostUserAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GhostUserAsync(userId, ct)
                 
        member this.GetUser (userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUser(userId, ct)
            
        member this.SetAppUserInfoAsync (userId: UserId, appUserInfo: AppUserInfo, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetAppUserInfoAsync(userId, appUserInfo, ct)