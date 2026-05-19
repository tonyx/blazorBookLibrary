
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
open Microsoft.Extensions.Logging

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
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userTenantResolverService: IUserTenantResolverService,
        distributionPointViewerAsync: AggregateViewerAsync2<DistributionPoint>,
        reviewService: IReviewService,
        scopeFactory: IServiceScopeFactory,
        logger: ILogger<UserService>)
    =
    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {

            let! tenantId = 
                userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    let checkIsGlobalAdminOrTenantManagerOrSelf (context: UserContext) (ct: CancellationToken) (userId: UserId)= 
        taskResult {
            let! tenantId = 
                userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrSelf tenant context userId
        }
    let checkIsGlobalAdminOrTenantManagerOrPublicTenant (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenantId = 
                userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrPublicTenant tenant context
        }

    member this.MakeUserDetailsRefresherAsync(context: UserContext, id: UserId, ?ct: CancellationToken) = 
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

                    let! tenantId = 
                        userTenantResolverService.GetTenantForUserAsync(context, ct)
                    let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd

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
                        reviewService.GetReviewsOfUserAsync(context, id, ct)
                        
                    let! currentTenant = tenantViewerAsync (Some ct) tenantId.Value |> TaskResult.map snd
                        
                    return 
                        {
                            User = user
                            AppUser = user.AppUserInfo
                            CurrentTenant = currentTenant
                            FutureReservations = reservationsAndBooks
                            CurrentLoans = loansAndBooks
                            BooksAndReviews = booksAndReviews
                        }
                }

    // user creation comes always from a safe caller
    member this.CreateUserAsync (_: UserContext, user: User, ?ct: CancellationToken) : Task<Result<unit, string>> =
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

    member this.GetUserAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult 
            {

                do! checkIsGlobalAdminOrTenantManagerOrSelf context ct userId

                let! user = userViewerAsync (Some ct) userId.Value |> TaskResult.map snd
                
                return user
            }

    member private 
        this.GetRefreshableUserDetailsAsync(context: UserContext, userId: UserId, ?ct:CancellationToken): TaskResult<RefreshableUserDetails, string> =
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let ct = ct |> Option.defaultValue CancellationToken.None
                    let refresher = this.MakeUserDetailsRefresherAsync(context, userId)

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

    member this.GetUserDetailsAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<UserDetails, string>> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! refreshableUserDetails =
                this.GetRefreshableUserDetailsAsync(context, userId, ct)
            return refreshableUserDetails.UserDetails
        }

    member private this.UpdateAppUserPropertyAsync (context: UserContext, userId: UserId, updateAction: ApplicationUser -> unit, ?ct: CancellationToken) : Task<Result<unit, string>> =
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

    member private this.UpdateAppUserAndAggregateAsync (context: UserContext, userId: UserId, updateAction: ApplicationUser -> unit, command: UserCommand, ?ct: CancellationToken) : Task<Result<unit, string>> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! res = this.UpdateAppUserPropertyAsync(context, userId, updateAction, ct)
            let! _ = 
                runAggregateCommandMdAsync<User, UserEvent, string>
                    userId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    command
                    (Some ct)
            
            return res
        }

    member private this.GdprGhostEvents (userId: UserId) =
        // predicate to detect events to be ghosted, i.e. replaced withGdprGhost command
        let predicate =
            fun (strEvent: string) ->
                result
                    {
                        let! event = UserEvent.Deserialize strEvent
                        return!
                            match event with
                                | NomeSet _ -> Ok true
                                | CognomeSet _ -> Ok true
                                | CodiceFiscaleSet _ -> Ok true
                                | PhoneNumberSet _ -> Ok true
                                | _ -> Ok false
                    }
        let replacement = UserEvent.GdprGhosted.Serialize
        eventStore.GDPRReplaceEventsByPredicate User.Version User.StorageName userId.Value  predicate replacement

    member this.SetFiscalCodeAsync (context: UserContext, userId: UserId, fiscalCode: FiscalCode, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> u.CodiceFiscale <- fiscalCode.Value), SetCodiceFiscale fiscalCode, ?ct = ct)

    member this.SetNameAsync (context: UserContext, userId: UserId, name: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> u.Nome <- name), SetNome name, ?ct = ct)

    member this.SetSurnameAsync (context: UserContext, userId: UserId, surname: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> u.Cognome <- surname), SetCognome surname, ?ct = ct)

    member this.SetPhoneNumberAsync (context: UserContext, userId: UserId, phoneNumber: PhoneNumber, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> u.PhoneNumber <- phoneNumber.Value), SetPhoneNumber phoneNumber, ?ct = ct)

    member this.SetIsPhysicallyIdentifiedAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> u.IsIdentifiedPhysically <- true), SetPhysicalIdentification, ?ct = ct)

    member this.UnSetIsPhysicallyIdentifiedAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> u.IsIdentifiedPhysically <- false), UnsetPhysicalIdentification, ?ct = ct)

    member this.GhostUserAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        taskResult {
            do! checkIsGlobalAdminOrTenantManagerOrSelf context (defaultArg ct CancellationToken.None) userId
            return! this.UpdateAppUserAndAggregateAsync(context, userId, (fun u -> 
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
        }

    // call this only from trusted client code
    member private this.GetUserUnsafeAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! user = userViewerAsync (ct |> Some) userId.Value |> TaskResult.map snd
                return user
            }
    
    member this.SetAppUserInfoAsync (context: UserContext, userId: UserId, appUserInfo: AppUserInfo, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let setAppUserInfoCommand = UserCommand.SetAppUserInfo appUserInfo
                let result =
                    runAggregateCommandMdAsync 
                        userId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        setAppUserInfoCommand
                        (ct |> Some)

                return! result
            }
    member this.GetDistributionPointsManagedByUserAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<List<DistributionPoint>, string>> = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = 
                    userTenantResolverService.GetTenantForUserAsync(context, ct)

                let! distributionPoints = 
                    StateView.getAllFilteredAggregateStatesAsync<DistributionPoint, DistributionPointEvent, string> 
                        (fun (dp: DistributionPoint) -> 
                            dp.ReferenceUsers |> List.exists (fun (id: UserId) -> id = userId) &&
                            dp.TenantId = tenantId
                        )
                        eventStore
                        (ct |> Some)
                return distributionPoints |> List.map snd
            }

    new(eventStore: IEventStore<string>, scopeFactory: IServiceScopeFactory, reviewService: IReviewService, userTenantResolverService: IUserTenantResolverService, logger: ILogger<UserService>) =
        UserService(
            eventStore,
            MessageSenders.NoSender,
            getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore,
            userTenantResolverService,
            getAggregateStorageFreshStateViewerAsync<DistributionPoint, DistributionPointEvent, string> eventStore,
            reviewService,
            scopeFactory,
            logger
        )

    member this.SetCurrentTenantAsync(context: UserContext, userId: UserId, tenantId: TenantId, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenant = tenantViewerAsync (Some ct) tenantId.Value |> TaskResult.map snd

            let! (eventId, user) = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore (Some ct) userId.Value

            let allowed =
                match context with
                | UserContext.Authenticated(u, roles) when roles |> List.contains(Role.Admin) -> true
                | _  when (tenant.OwnerId = userId) -> true
                | _  when (tenant.Patrons |> List.exists (fun (u, _) -> u = userId)) -> true
                | _ -> false
            do! 
                if not allowed then Error "Not allowed to set current tenant" else Ok ()
            let setTenantCommand = SetCurrentTenant tenantId
            let result =
                runAggregateCommandMdAsync
                    userId.Value
                    eventStore
                    messageSenders
                    (context.ToString())
                    setTenantCommand
                    (Some ct)
            return! result
        }

    member this.SetAppUserInfoUnsafeAsync (userId: UserId, appUserInfo: AppUserInfo, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let setAppUserInfoCommand = UserCommand.SetAppUserInfo appUserInfo
                let result =
                    runAggregateCommandMdAsync 
                        userId.Value
                        eventStore
                        messageSenders
                        "System"
                        setAppUserInfoCommand
                        (ct |> Some)

                return! result
            }

    member this.GetUserIdByEmailAsync (context: UserContext, email: string, ?ct: CancellationToken) : Task<Result<UserId, string>> =
        let ct = defaultArg ct CancellationToken.None
        taskResult {
            match context with
            | UserContext.Anonymous -> 
                return! Error "Anonymous users are not allowed to search users"
            | UserContext.Authenticated(_, _) ->
                use scope = scopeFactory.CreateScope()
                let userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>()
                let! appUser = 
                    task {
                        let! user = userManager.FindByEmailAsync(email)
                        if user <> null then return Ok user else return Error (sprintf "User with email %s not found" email)
                    }
                return UserId (Guid.Parse(appUser.Id))
        }

    new(configuration: IConfiguration, scopeFactory: IServiceScopeFactory, secretsReader: BookLibrary.Utils.SecretsReader, reviewService: IReviewService, userTenantResolverService: IUserTenantResolverService, logger: ILogger<UserService>) =
        UserService(PgStorage.PgEventStore (secretsReader.GetBookLibraryConnectionString ()), scopeFactory, reviewService, userTenantResolverService, logger)

    interface IUserService with
        member this.CreateUserAsync (context, user: User, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.CreateUserAsync(context, user, ct)
        member this.GetUserAsync (context, userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUserAsync(context, userId, ct)
        member this.GetUserDetailsAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) : Task<Result<UserDetails, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUserDetailsAsync(context, userId, ct)
        member this.SetFiscalCodeAsync (context, userId: UserId, fiscalCode: FiscalCode, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetFiscalCodeAsync(context, userId, fiscalCode, ct)
        member this.SetNameAsync (context, userId: UserId, name: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetNameAsync(context, userId, name, ct)
        member this.SetSurnameAsync (context, userId: UserId, surname: string, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetSurnameAsync(context, userId, surname, ct)
        member this.SetPhoneNumberAsync (context, userId: UserId, phoneNumber: PhoneNumber, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetPhoneNumberAsync(context, userId, phoneNumber, ct)
        member this.SetIsPhysicallyIdentifiedAsync (context, userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetIsPhysicallyIdentifiedAsync(context, userId, ct)
        member this.UnSetIsPhysicallyIdentifiedAsync (context, userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.UnSetIsPhysicallyIdentifiedAsync(context, userId, ct)
        member this.GhostUserAsync (context, userId: UserId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GhostUserAsync(context, userId, ct)
        member this.GetUserUnsafeAsync (userId: UserId, ?ct: CancellationToken) : Task<Result<User, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUserUnsafeAsync(userId, ct)
        member this.SetAppUserInfoAsync (context, userId: UserId, appUserInfo: AppUserInfo, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetAppUserInfoAsync(context, userId, appUserInfo, ct)

        member this.SetAppUserInfoUnsafeAsync (userId: UserId, appUserInfo: AppUserInfo, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetAppUserInfoUnsafeAsync(userId, appUserInfo, ct)
        member this.GetDistributionPointsManagedByUserAsync (context, userId: UserId, ?ct: CancellationToken) : Task<Result<List<DistributionPoint>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetDistributionPointsManagedByUserAsync(context, userId, ct)
        member this.SetCurrentTenantAsync (context, userId: UserId, tenantId: TenantId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SetCurrentTenantAsync(context, userId, tenantId, ct)
        member this.GetUserIdByEmailAsync (context, email: string, ?ct: CancellationToken) : Task<Result<UserId, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.GetUserIdByEmailAsync(context, email, ct)