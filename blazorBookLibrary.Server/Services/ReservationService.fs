
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open BookLibrary.Details.Details
open System.Globalization
open blazorBookLibrary.Shared.Infrastructure.Services
open BookLibrary.Utils

type ReservationService
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
        usersService: IUserService,
        mailNotificator: IMailNotificator,
        maxReservations: int,
        fromEmail: string,
        fromName: string,
        mailBodyRetriever: IMailBodyRetriever
    ) =
    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenantId = 
                userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }
    let checkIsGlobalAdminOrTenantManagerOrPublicTenant (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenantId = 
                userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManagerOrPublicTenant tenant context
        }

    new (eventStore: IEventStore<string>, userService: IUserService, mailNotificator: IMailNotificator, configuration: IConfiguration, mailBodyRetriever: IMailBodyRetriever, userTenantResolverService: IUserTenantResolverService)
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let tenantViewerAsync = getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore
        let distributionPointViewerAsync = getAggregateStorageFreshStateViewerAsync<DistributionPoint, DistributionPointEvent, string> eventStore
        let maxReservations = configuration.GetValue<int>("BooksLibrary:MaxReservationsPerUser", 3)
        let fromEmail = configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com")
        let fromName = configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library")
        ReservationService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            tenantViewerAsync,
            distributionPointViewerAsync,
            userTenantResolverService,
            userService,
            mailNotificator,
            maxReservations,
            fromEmail,
            fromName,
            mailBodyRetriever
        )
    new (configuration: IConfiguration, userService: IUserService, mailNotificator: IMailNotificator, mailBodyRetriever: IMailBodyRetriever, secretsReader: SecretsReader, userTenantResolverService: IUserTenantResolverService) 
        =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        ReservationService(eventStore, userService, mailNotificator, configuration, mailBodyRetriever, userTenantResolverService) 

        member private this.MakeReservationRefresher(context: UserContext, id: ReservationId, ?ct:CancellationToken) = 
            fun (ct: Option<CancellationToken>) ->
                taskResult
                    {
                        let ct = ct |> Option.defaultValue CancellationToken.None
                        let! reservation = 
                            reservationViewerAsync (ct |> Some) id.Value |> TaskResult.map snd
                        let! tenantId = 
                            userTenantResolverService.GetTenantForUserAsync(context, ct)
                        let! book = 
                            bookViewerAsync (ct |> Some) reservation.BookId.Value |> TaskResult.map snd
                        let! userDetails = 
                            usersService.GetUserDetailsAsync (context, reservation.UserId, ct)
                        do!
                            book.TenantId = tenantId
                            |> Result.ofBool $"Book tenant id {book.TenantId} does not match user tenant id {tenantId}"
                        return 
                            {
                                Reservation = reservation
                                Book = book
                                UserDetails = userDetails
                            }
                    }

        member this.MakeReservationDetailsBuilder(id: ReservationId, refresher: Option<CancellationToken> -> TaskResult<ReservationDetails, string>) = 
            taskResult {
                let! reservationDetails = refresher (Some CancellationToken.None)
                return 
                    {
                        ReservationDetails = reservationDetails    
                        Refresher = refresher
                    } :> RefreshableAsync<RefreshableReservationDetails>
                    ,
                    [id.Value ;
                    reservationDetails.Reservation.BookId.Value ;
                    reservationDetails.Book.BookId.Value]
                }
            
        member this.AddReservationAsync (context: UserContext, reservation: Reservation, dateTime: System.DateTime, shortLang: ShortLang, ?ct: CancellationToken) = 

            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! book =
                        bookViewerAsync (Some ct) reservation.BookId.Value
                        |> TaskResult.map snd
                    let! user =
                        userViewerAsync (Some ct) reservation.UserId.Value
                        |> TaskResult.map snd

                    let! userHasEnoughReservations = 
                        user.Reservations.Length < maxReservations
                        |> Result.ofBool "Already reached maximum number of reservations"
                    do!
                        reservation.TimeSlot.IsFutureOf(dateTime)
                        |> Result.ofBool "Reservation time slot must be in the future"

                    let! alreadyExistingReservations =
                        this.GetReservationsAsync (context, book.CurrentReservations)
                    let! tenantId = 
                        userTenantResolverService.GetTenantForUserAsync(context, ct)

                    do! 
                        tenantId = book.TenantId
                        |> Result.ofBool $"Book tenant id {book.TenantId} does not match user tenant id {tenantId}"

                    let! (_, tenant) =
                        tenantViewerAsync (ct |> Some) tenantId.Value

                    let! noOverlaps =
                        alreadyExistingReservations
                        |> List.forall (fun r -> not (r.TimeSlot.Overlaps(reservation.TimeSlot)))
                        |> Result.ofBool "Reservation overlaps with existing reservation"

                    let addReservationToBookCommand = 
                        BookCommand.AddReservation (reservation.ReservationId, dateTime)

                    let addReservationToUserCommand = 
                        UserCommand.AddReservation reservation.ReservationId

                    let! userDetails = 
                        usersService.GetUserDetailsAsync (context, user.UserId, ct)

                    let! optDpName = 
                        match book.DistributionPoint with
                        | None -> 
                            task
                                { return "Unspecified" |> Ok}
                           
                        | Some dpId -> 
                            taskResult
                                { 
                                    let! (_, dp) =
                                        distributionPointViewerAsync (ct |> Some) dpId.Value
                                    return dp.Name.Value 
                                }

                    let! emailTextRetrieved = 
                        mailBodyRetriever.GetReservationNotificationTextMailAsync(
                            book.Title,
                            reservation.ReservationCode,
                            tenant.Name,
                            optDpName,
                            user.LangPref,
                            ?ct = Some ct
                        )
                    let! emailSubjectRetrieved = 
                        mailBodyRetriever.GetReservationNotificationSubject(user.LangPref, ?ct = Some ct)
                

                    let! result =
                        runInitAndTwoAggregateCommandsMdAsync<Book, BookEvent, User, UserEvent, string, Reservation>
                            book.Id
                            user.Id
                            eventStore
                            messageSenders
                            reservation
                            ""
                            addReservationToBookCommand
                            addReservationToUserCommand
                            (Some ct)

                    let emailBody = emailTextRetrieved

                    let emailSubject = emailSubjectRetrieved.Replace("{bookTitle}", book.Title.Value)

                    let key = DetailsCacheKey.OfType typeof<RefreshableTenantDetails> reservation.Id
                    let updateDetails =
                           DetailsCache.Instance.UpdateMultipleAggregateIdAssociation [|reservation.Id|] key
                    
                    do! 
                        task {
                            do! mailNotificator.SendEmailAsync(
                                    fromEmail,
                                    fromName,
                                    userDetails.AppUser.Email,
                                    emailSubject,
                                    emailBody
                                )

                            return Ok ()
                        }

                    return result
                }

        member this.AddReservationAsync (context: UserContext, reservation: Reservation, dateTime: DateTime, ?ct: CancellationToken) =
            this.AddReservationAsync (context, reservation, dateTime, ShortLang.New "en", ?ct = ct)

        member this.GetAllReservationsAsync (context: UserContext, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! tenantId = 
                        userTenantResolverService.GetTenantForUserAsync(context, ct)
                    let! reservations =
                        StateView.getAllAggregateStatesAsync<Reservation, ReservationEvent, string> eventStore (Some ct)
                        |> TaskResult.map (fun reservations -> reservations |> List.map snd)
                    return 
                        reservations
                        |> List.filter (fun r -> r.TenantId = tenantId)
                }

    member this.GetReservationAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = 
                    userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! result = 
                    reservationViewerAsync (Some ct) id.Value
                    |> TaskResult.map snd
                do!
                    result.TenantId = tenantId
                    |> Result.ofBool $"Reservation tenant id {result.TenantId} does not match user tenant id {tenantId}"
                return result
            }
    member this.GetRefreshableReservationDetailsAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                this.MakeReservationRefresher(context, id, ct|> Option.defaultValue CancellationToken.None) 
                |> fun refresher -> this.MakeReservationDetailsBuilder(id, refresher)

        let key = DetailsCacheKey.OfType typeof<RefreshableReservationDetails> id.Value
        StateView.getRefreshableDetailsTaskResultAsync<RefreshableReservationDetails> (fun ct -> detailsBuilder ct) key ct

    member this.RemoveReservationAsync (context: UserContext, reservationId: ReservationId, dateTime: System.DateTime, ?ct:CancellationToken)= 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! reservation = 
                    this.GetReservationAsync(context, reservationId, ct)

                let! tenantId = 
                    userTenantResolverService.GetTenantForUserAsync(context, ct)
                
                let! book =
                    bookViewerAsync (Some ct) reservation.BookId.Value
                    |> TaskResult.map snd
                let! user =     
                    userViewerAsync (Some ct) reservation.UserId.Value
                    |> TaskResult.map snd

                let removeReservationFromBook: AggregateCommand<Book, BookEvent> =
                    BookCommand.RemoveReservation (reservation.ReservationId, dateTime)

                let removeReservationFromUser: AggregateCommand<User, UserEvent> =
                    UserCommand.RemoveReservation reservation.ReservationId

                let! result =
                    runDeleteAndTwoAggregateCommandsMd<Reservation, ReservationEvent, Book, BookEvent, User, UserEvent, string>
                        eventStore
                        messageSenders
                        ""
                        reservationId.Value
                        book.Id
                        user.Id
                        removeReservationFromBook
                        removeReservationFromUser
                        (fun _ -> true)
                return result
            }

    member this.GetReservationsAsync (context: UserContext, ids: List<ReservationId>, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result = 
                    ids
                    |> List.traverseTaskResultM (fun id -> this.GetReservationAsync (context, id, ct))
                return result
            }
    member this.RemoveExpiredReservationsAsync (context: UserContext, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! tenantId = 
                    userTenantResolverService.GetTenantForUserAsync(context, ct)
                let now = DateTime.UtcNow
                let! expiredReservations = 
                    StateView.getAllFilteredAggregateStatesAsync<Reservation, ReservationEvent, string> 
                        (fun reservation -> reservation.IsExpired now && reservation.TenantId = tenantId) eventStore (Some ct)
                    |> TaskResult.map (fun reservations -> reservations |> List.map snd)
                let! result = 
                    expiredReservations
                    |> List.traverseTaskResultM (fun reservation -> this.RemoveReservationAsync (context, reservation.ReservationId, now, ct))
                return ()
            }

    member this.GeneratePickupPinAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = 
                    userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! reservation = 
                    this.GetReservationAsync (context, id, ct)
                
                let isPatron = 
                    match context.UserId with
                    | Some uid -> uid = reservation.UserId
                    | None -> false
                
                let! _ = 
                    if isPatron then TaskResult.ok ()
                    else checkIsGlobalAdminOrTenantManager context ct

                let rnd = System.Security.Cryptography.RandomNumberGenerator.GetInt32(100000, 999999)
                let pin = string rnd
                
                let pinBytes = System.Text.Encoding.UTF8.GetBytes(pin)
                let hashBytes = System.Security.Cryptography.SHA256.HashData(pinBytes)
                let pinHash = System.Convert.ToHexString(hashBytes).ToLowerInvariant()
                
                let expiresAt = DateTime.UtcNow.AddMinutes(15.0)
                
                let generatePinCommand = ReservationCommand.GeneratePickupPin(pinHash, expiresAt)
                
                let! _ =
                    runAggregateCommandMdAsync<Reservation, ReservationEvent, string>
                        id.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        generatePinCommand
                        (Some ct)
                
                return (pin, expiresAt)
            }

    interface IReservationService with
        member this.AddReservationAsync (context: UserContext, reservation: Reservation, shortLang: ShortLang, ?ct: CancellationToken)= 
            let ct = defaultArg ct CancellationToken.None
            this.AddReservationAsync (context, reservation, DateTime.UtcNow, shortLang, ct)
        member this.GetReservationAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetReservationAsync (context, id, ct)
        member this.GetReservationDetailsAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! refreshableDetails = this.GetRefreshableReservationDetailsAsync (context, id, ct)
                    return refreshableDetails.ReservationDetails
                }
        member this.RemoveReservationAsync (context: UserContext, reservationId: ReservationId, ?ct:CancellationToken)= 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveReservationAsync (context, reservationId, DateTime.UtcNow, ct)            
        member this.GetReservationsAsync(context: UserContext, ids: List<ReservationId>, ?ct: CancellationToken)= 
            let ct = defaultArg ct CancellationToken.None
            this.GetReservationsAsync (context, ids, ct)
        member this.RemoveExpiredReservationsAsync (context: UserContext, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveExpiredReservationsAsync (context, ct)
        member this.GeneratePickupPinAsync (context: UserContext, id: ReservationId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GeneratePickupPinAsync (context, id, ct)

        member this.GetAllPendingReservationsDetailsAsync (context: UserContext, ?ct: CancellationToken) = 
            taskResult
                {
                    let ct = defaultArg ct CancellationToken.None
                    let! tenantId = 
                        userTenantResolverService.GetTenantForUserAsync(context, ct)
                    let! reservations = 
                        StateView.getAllFilteredAggregateStatesAsync<Reservation, ReservationEvent, string> 
                            (fun reservation -> reservation.IsPending && reservation.TenantId = tenantId) eventStore (Some ct)
                        |> TaskResult.map (fun reservations -> reservations |> List.map snd)
                    let! reservationDetails = 
                        reservations
                        |> List.traverseTaskResultM (fun reservation -> (this:>IReservationService).GetReservationDetailsAsync (context, reservation.ReservationId, ct))
                    return reservationDetails 
                }