namespace BookLibrary.Services

open System.Threading
open Microsoft.Extensions.Configuration
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
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
open blazorBookLibrary.Shared.Infrastructure.Services
open blazorBookLibrary.Shared.Resources
open Microsoft.Extensions.Localization
open System.Globalization
open BookLibrary.Utils

type LoanService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        userViewerAsync: AggregateViewerAsync2<User>,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        distributionPointViewerAsync: AggregateViewerAsync2<DistributionPoint>,
        userTenantResolverService: IUserTenantResolverService,
        reservationService: IReservationService,
        usersService: IUserService,
        notificationDispatcher: INotificationDispatcher,
        maxLoanPerUser: int,
        fromEmail: string,
        fromName: string,
        localizer: IStringLocalizer<SharedResources>,
        mailBodyRetriever: IMailBodyRetriever
    ) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    let checkIsGlobalAdminOrTenantManagerOrSelf (context: UserContext) (ct: CancellationToken) (userId: UserId) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrSelf tenant context userId
        }

    member this.AddLoanAsync(context: UserContext, loan: Loan, dateTime: System.DateTime, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! book = bookViewerAsync (Some ct) loan.BookId.Value |> TaskResult.map snd

            do!
                book.TenantId = tenantId
                |> Result.ofBool $"Book tenant id {book.TenantId} does not match user tenant id {tenantId}"

            let! user = userViewerAsync (Some ct) loan.UserId.Value |> TaskResult.map snd

            do! checkIsGlobalAdminOrTenantManager context ct

            let! userDetails = usersService.GetUserDetailsAsync(context, user.UserId, ct)

            let! (_, tenant) = tenantViewerAsync (ct |> Some) tenantId.Value

            let! optDpName =
                match book.DistributionPoint with
                | None -> taskResult { return (localizer.GetString("Unspecified").Value) }

                | Some dpId ->
                    taskResult {
                        let! (_, dp) = distributionPointViewerAsync (ct |> Some) dpId.Value
                        return dp.Name.Value
                    }

            let setCurrentLoanCommand = BookCommand.SetCurrentLoan(loan.LoanId, dateTime)

            let! result =
                runInitAndAggregateCommandMdAsync<Book, BookEvent, Loan, string>
                    book.Id
                    eventStore
                    messageSenders
                    loan
                    ""
                    setCurrentLoanCommand
                    (ct |> Some)

            let! _ =
                notificationDispatcher.DispatchNotificationAsync(
                    context,
                    loan.UserId,
                    tenantId,
                    Some $"/loans/{loan.Id}",
                    ct
                )

            // not sure this is needed
            let! _ = DetailsCache.Instance.RefreshDependentDetailsAsync(loan.UserId.Value, Some ct)
            return result
        }

    member this.GetLoanAsync(context: UserContext, id: LoanId, ?ct: CancellationToken) : TaskResult<Loan, string> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! loan = loanViewerAsync (Some ct) id.Value |> TaskResult.map snd
            let userId = loan.UserId
            do! checkIsGlobalAdminOrTenantManagerOrSelf context ct userId
            let! result = loanViewerAsync (Some ct) id.Value
            return result |> snd
        }

    member this.GetRefreshableLoanDetailsAsync
        (context: UserContext, loanId: LoanId, ?ct: CancellationToken)
        : TaskResult<RefreshableLoanDetails, string> =
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None

                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                            let! loan = loanViewerAsync (ct |> Some) loanId.Value |> TaskResult.map snd
                            let! book = bookViewerAsync (ct |> Some) loan.BookId.Value |> TaskResult.map snd
                            let! userDetail = usersService.GetUserDetailsAsync(context, loan.UserId, ct)

                            do!
                                book.TenantId = tenantId
                                |> Result.ofBool
                                    $"Book tenant id {book.TenantId} does not match user tenant id {tenantId}"

                            return
                                { Loan = loan
                                  Book = book
                                  UserDetails = userDetail }
                        }

                taskResult {
                    let! loanDetails = refresher (Some ct)

                    return
                        { LoanDetails = loanDetails
                          Refresher = refresher }
                        :> RefreshableAsync<RefreshableLoanDetails>,
                        [ loanId.Value; loanDetails.Book.Id; loanDetails.UserDetails.User.Id ]
                }

        let key = DetailsCacheKey.OfType typeof<RefreshableLoanDetails> loanId.Value
        StateView.getRefreshableDetailsTaskResultAsync<RefreshableLoanDetails> (fun ct -> detailsBuilder ct) key ct

    member this.GetLoansAsync(context: UserContext, ?ct: CancellationToken) : TaskResult<List<Loan>, string> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            let! result =
                StateView.getAllAggregateStatesAsync<Loan, LoanEvent, string> eventStore (ct |> Some)
                |> TaskResult.map (fun x -> x |> List.map snd)

            return result |> List.filter (fun l -> l.TenantId = tenantId)
        }

    member this.GetLoansOfUserInATenantAsync
        (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken)
        : TaskResult<List<Loan>, string> =
        // todo: add a security check
        taskResult {
            let ct = defaultArg ct CancellationToken.None

            let! result =
                StateView.getAllFilteredAggregateStatesAsync<Loan, LoanEvent, string>
                    (fun (loan: Loan) -> loan.TenantId = tenantId && loan.UserId = userId)
                    eventStore
                    (ct |> Some)
                |> TaskResult.map (fun x -> x |>> snd)

            return result
        }

    member this.GetUnarchivedLoansAsync(context: UserContext, ?ct: CancellationToken) : TaskResult<List<Loan>, string> =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)

            let isUnarchivedForTenant (loan: Loan) =
                loan.TenantId = tenantId && not loan.LoanStatus.IsArchived

            let! result =
                StateView.getAllFilteredAggregateStatesAsync<Loan, LoanEvent, string>
                    isUnarchivedForTenant
                    eventStore
                    (ct |> Some)
                |> TaskResult.map (fun x -> x |> List.map snd)

            return result
        }

    member this.ReleaseLoanAsync
        (context: UserContext, loanId: LoanId, dateTime: System.DateTime, ?ct: CancellationToken)
        =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! loan = loanViewerAsync (Some ct) loanId.Value |> TaskResult.map snd
            let! book = bookViewerAsync (Some ct) loan.BookId.Value |> TaskResult.map snd
            let! (_, tenant) = tenantViewerAsync (Some ct) loan.TenantId.Value

            let! distributionPointName =
                match book.DistributionPoint with
                | Some distributionPointId ->
                    distributionPointViewerAsync (Some ct) distributionPointId.Value
                    |> TaskResult.map (fun (_, x) -> x.Name)
                | None -> taskResult { return NonEmptyName(localizer.GetString("Unspecified").Value) }

            let! user = userViewerAsync (Some ct) loan.UserId.Value |> TaskResult.map snd
            let releaseLoanCommand = BookCommand.ReleaseLoan(loanId, dateTime)
            let releaseBookCommand = LoanCommand.Return dateTime
            let userReleaseLoanCommandr = UserCommand.ReleaseLoan(loanId)
            let! result =
                runThreeAggregateCommandsMdAsync<Book, BookEvent, Loan, LoanEvent, User, UserEvent, string>
                    book.Id
                    loan.Id
                    user.Id
                    eventStore
                    messageSenders
                    ""
                    releaseLoanCommand
                    releaseBookCommand
                    userReleaseLoanCommandr
                    (ct |> Some)

            let! _ =
                notificationDispatcher.DispatchNotificationAsync(
                    context,
                    loan.UserId,
                    loan.TenantId,
                    Some $"/loans/{loan.Id}/release",
                    ct
                )

            let! _ = DetailsCache.Instance.RefreshDependentDetailsAsync(loan.UserId.Value, Some ct)
            return result
        }

    member this.GetHistoryLoansOfUserAsync(context: UserContext, userId: UserId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            do! checkIsGlobalAdminOrTenantManagerOrSelf context ct userId

            let! loans =
                StateView.getAllFilteredAggregateStatesAsync<Loan, LoanEvent, string>
                    (fun loan -> loan.UserId = userId && loan.TenantId = tenantId)
                    eventStore
                    (ct |> Some)
                |> TaskResult.map (fun x -> x |> List.map snd)

            return loans
        }

    member this.TransformReservationIntoLoanAsync
        (
            context: UserContext,
            reservationId: ReservationId,
            providedReservationCode: ReservationCode,
            now: DateTime,
            ?ct: CancellationToken
        ) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! reservation = reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd
            let! reservationDetails = reservationService.GetReservationDetailsAsync(context, reservationId, ct)

            let book = reservationDetails.Book

            do! book.NoLoan |> Result.ofBool "Book is already loaned"

            let! matchReservationCode =
                reservation.ReservationCode = providedReservationCode
                |> Result.ofBool "Reservation code must match"

            let makeReservationLoaned = ReservationCommand.Loan now

            let! loan = reservationDetails.ToLoan now

            let setBookLoaned =
                BookCommand.SetCurrentLoanFromReservation(reservationId, loan.LoanId, now)

            let makeLoanFromReservation =
                UserCommand.LoanFromReservation(loan.LoanId, reservationId)

            let! optDpName =
                match book.DistributionPoint with
                | None -> taskResult { return (localizer.GetString("Unspecified").Value) }

                | Some dpId ->
                    taskResult {
                        let! (_, dp) = distributionPointViewerAsync (ct |> Some) dpId.Value
                        return dp.Name.Value
                    }

            let! result =
                runInitAndThreeAggregateCommandsMdAsync<
                    Reservation,
                    ReservationEvent,
                    Book,
                    BookEvent,
                    User,
                    UserEvent,
                    string,
                    Loan
                 >
                    reservation.Id
                    book.Id
                    reservationDetails.UserDetails.User.Id
                    eventStore
                    messageSenders
                    loan
                    ""
                    makeReservationLoaned
                    setBookLoaned
                    makeLoanFromReservation
                    (ct |> Some)

            let key = DetailsCacheKey.OfType typeof<RefreshableTenantDetails> reservation.Id

            let updateDetails =
                DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [| loan.Id |] key

            let! _ =
                notificationDispatcher.DispatchNotificationAsync(
                    context,
                    reservation.UserId,
                    reservation.TenantId,
                    Some $"/loans/{loan.Id}",
                    ct
                )

            let! _ = DetailsCache.Instance.RefreshDependentDetailsAsync(reservation.BookId.Value, Some ct)
            let! _ = DetailsCache.Instance.RefreshDependentDetailsAsync(reservation.UserId.Value, Some ct)
            return result
        }

    member this.TransformReservationIntoLoanByPinAsync
        (context: UserContext, reservationId: ReservationId, pin: string, now: DateTime, ?ct: CancellationToken)
        =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! reservation = reservationViewerAsync (Some ct) reservationId.Value |> TaskResult.map snd
            let! reservationDetails = reservationService.GetReservationDetailsAsync(context, reservationId, ct)

            let book = reservationDetails.Book

            do! book.NoLoan |> Result.ofBool "Book is already loaned"

            // Compute the SHA-256 hash of the input PIN to match the GeneratePickupPin implementation.
            let pinBytes = System.Text.Encoding.UTF8.GetBytes(pin)
            let hashBytes = System.Security.Cryptography.SHA256.HashData(pinBytes)
            let pinHash = System.Convert.ToHexString(hashBytes).ToLowerInvariant()

            // VerifyPickupPin command validates the PIN hash and expiration time.
            let verifyPickupPin = ReservationCommand.VerifyPickupPin(pinHash, now)

            let! loan = reservationDetails.ToLoan now

            let setBookLoaned =
                BookCommand.SetCurrentLoanFromReservation(reservationId, loan.LoanId, now)

            let makeLoanFromReservation =
                UserCommand.LoanFromReservation(loan.LoanId, reservationId)

            let! optDpName =
                match book.DistributionPoint with
                | None -> taskResult { return (localizer.GetString("Unspecified").Value) }

                | Some dpId ->
                    taskResult {
                        let! (_, dp) = distributionPointViewerAsync (ct |> Some) dpId.Value
                        return dp.Name.Value
                    }

            let! result =
                runInitAndThreeAggregateCommandsMdAsync<
                    Reservation,
                    ReservationEvent,
                    Book,
                    BookEvent,
                    User,
                    UserEvent,
                    string,
                    Loan
                 >
                    reservation.Id
                    book.Id
                    reservationDetails.UserDetails.User.Id
                    eventStore
                    messageSenders
                    loan
                    ""
                    verifyPickupPin
                    setBookLoaned
                    makeLoanFromReservation
                    (ct |> Some)

            let key = DetailsCacheKey.OfType typeof<RefreshableTenantDetails> reservation.Id

            let updateDetails =
                DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [| loan.Id |] key

            let! _ =
                notificationDispatcher.DispatchNotificationAsync(
                    context,
                    reservation.UserId,
                    reservation.TenantId,
                    Some $"/loans/{loan.Id}",
                    ct
                )

            // todo: check if this is needed
            let! _ = DetailsCache.Instance.RefreshDependentDetailsAsync(reservation.UserId.Value, Some ct)
            return result
        }

    member this.RemoveLoanAsync(context: UserContext, loanId: LoanId, ?ct: CancellationToken) =
        let ct = defaultArg ct CancellationToken.None

        taskResult {
            let! (_, loan) = loanViewerAsync (ct |> Some) loanId.Value
            let! (_, tenant) = tenantViewerAsync (ct |> Some) loan.TenantId.Value

            do!
                match context with
                | UserContext.Authenticated(_, roles) when roles |> List.contains (Role.Admin) -> Ok()
                | _ -> Error "User is not authorized to remove loan"

            let predicate = fun (loan: Loan) -> not loan.InProgress

            let! result =
                runDeleteAsync<Loan, LoanEvent, string> eventStore messageSenders loanId.Value predicate (ct |> Some)

            return result
        }

    member this.ArchiveLoanAsync(context: UserContext, loanId: LoanId, ?ct: CancellationToken) =
        taskResult {
            let ct = defaultArg ct CancellationToken.None
            let! (_, loan) = loanViewerAsync (ct |> Some) loanId.Value
            let! (_, tenant) = tenantViewerAsync (ct |> Some) loan.TenantId.Value

            do!
                match context with
                | UserContext.Authenticated(_, roles) when roles |> List.contains (Role.Admin) -> Ok()
                | UserContext.Authenticated(userId, _) when userId = tenant.OwnerId -> Ok()
                | _ -> Error "User is not authorized to archive loan"

            let archiveCommand = LoanCommand.Archive(System.DateTime.UtcNow)

            let! result =
                runAggregateCommandMdAsync<Loan, LoanEvent, string>
                    loan.Id
                    eventStore
                    messageSenders
                    (context.ToString())
                    archiveCommand
                    (ct |> Some)

            let key =
                DetailsCacheKey.OfType typeof<RefreshableTenantDetails> loan.TenantId.Value

            let refresh = DetailsCache.Instance.RefreshAsync(key, ct |> Some)

            return result
        }

    new
        (
            eventStore: IEventStore<string>,
            reservationService: IReservationService,
            usersService: IUserService,
            notificationDispatcher: INotificationDispatcher,
            localizer: IStringLocalizer<SharedResources>,
            configuration: IConfiguration,
            mailBodyRetriever: IMailBodyRetriever,
            userTenantResolverService: IUserTenantResolverService
        ) =
        LoanService(
            eventStore,
            MessageSenders.NoSender,
            getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore,
            getAggregateStorageFreshStateViewerAsync<DistributionPoint, DistributionPointEvent, string> eventStore,
            userTenantResolverService,
            reservationService,
            usersService,
            notificationDispatcher,
            configuration.GetValue<int>("BooksLibrary:MaxLoanPerUser", 3),
            configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com"),
            configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library"),
            localizer,
            mailBodyRetriever
        )

    new
        (
            configuration: IConfiguration,
            reservationService: IReservationService,
            usersService: IUserService,
            notificationDispatcher: INotificationDispatcher,
            localizer: IStringLocalizer<SharedResources>,
            mailBodyRetriever: IMailBodyRetriever,
            secretsReader: SecretsReader,
            userTenantResolverService: IUserTenantResolverService
        ) =
        LoanService(
            PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString()),
            reservationService,
            usersService,
            notificationDispatcher,
            localizer,
            configuration,
            mailBodyRetriever,
            userTenantResolverService
        )

    new
        (
            connectionString: string,
            reservationService: IReservationService,
            usersService: IUserService,
            notificationDispatcher: INotificationDispatcher,
            localizer: IStringLocalizer<SharedResources>,
            configuration: IConfiguration,
            mailBodyRetriever: IMailBodyRetriever,
            userTenantResolverService: IUserTenantResolverService
        ) =
        LoanService(
            PgStorage.PgEventStore connectionString,
            reservationService,
            usersService,
            notificationDispatcher,
            localizer,
            configuration,
            mailBodyRetriever,
            userTenantResolverService
        )

    interface ILoanService with
        member this.AddLoanAsync(context: UserContext, loan: Loan, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.AddLoanAsync(context, loan, System.DateTime.Now, ct)

        member this.GetLoanAsync(context: UserContext, id: LoanId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetLoanAsync(context, id, ct)

        member this.GetUnarchivedLoansAsync(context: UserContext, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetUnarchivedLoansAsync(context, ct)

        member this.ReleaseLoanAsync(context: UserContext, loanId: LoanId, now: DateTime, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.ReleaseLoanAsync(context, loanId, now, ct)

        member this.GetLoansAsync(context: UserContext, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetLoansAsync(context, ct)

        member this.GetLoansOfUserInATenantAsync
            (context: UserContext, tenantId: TenantId, userId: UserId, ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            this.GetLoansOfUserInATenantAsync(context, tenantId, userId, ct)

        member this.GetHistoryLoansOfUserAsync(context: UserContext, userId: UserId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetHistoryLoansOfUserAsync(context, userId, ct)

        member this.TransformReservationIntoLoanAsync
            (
                context: UserContext,
                reservationId: ReservationId,
                providedReservationCode: ReservationCode,
                now: DateTime,
                ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            this.TransformReservationIntoLoanAsync(context, reservationId, providedReservationCode, now, ct)

        member this.TransformReservationIntoLoanByPinAsync
            (context: UserContext, reservationId: ReservationId, pin: string, now: DateTime, ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            this.TransformReservationIntoLoanByPinAsync(context, reservationId, pin, now, ct)

        member this.RemoveLoanAsync(context: UserContext, loanId: LoanId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveLoanAsync(context, loanId, ct)

        member this.ArchiveLoanAsync(context: UserContext, loanId: LoanId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.ArchiveLoanAsync(context, loanId, ct)
