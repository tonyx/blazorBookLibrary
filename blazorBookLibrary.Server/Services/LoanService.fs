
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
        userTenantResolverService: IUserTenantResolverService,
        reservationService: IReservationService,
        usersService: IUserService,
        mailNotificator: IMailNotificator,
        maxLoanPerUser: int,
        fromEmail: string,
        fromName: string,
        localizer: IStringLocalizer<SharedResources>,
        mailBodyRetriever: IMailBodyRetriever

    ) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken)= 
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    member this.AddLoanAsync (context: UserContext, loan: Loan, shortLang: ShortLang, dateTime: System.DateTime, ?ct: CancellationToken)= 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! book = 
                    bookViewerAsync (Some ct) loan.BookId.Value 
                    |> TaskResult.map snd
                do!
                    book.TenantId = tenantId
                    |> Result.ofBool $"Book tenant id {book.TenantId} does not match user tenant id {tenantId}"
                    
                let! user =
                    userViewerAsync (Some ct) loan.UserId.Value
                    |> TaskResult.map snd
                
                let! userDetails = 
                    usersService.GetUserDetailsAsync (context, user.UserId, ct)
                
                let setCurrentLoanCommand = 
                    BookCommand.SetCurrentLoan (loan.LoanId, dateTime)

                let addLoanToUser =     
                    UserCommand.AddLoan (loan.LoanId)

                let! emailTextRetrieved = 
                    mailBodyRetriever.GetLoanNotificationTextMailAsync(shortLang)

                let! result = 
                    runInitAndTwoAggregateCommandsMdAsync<Book, BookEvent, User, UserEvent, string, Loan>
                        book.Id
                        user.Id
                        eventStore
                        messageSenders
                        loan
                        ""
                        setCurrentLoanCommand
                        addLoanToUser
                        (ct |> Some)

                let emailBody = emailTextRetrieved.Replace("{bookTitle}",book.Title.Value).Replace("{loanedAt}",dateTime.ToString("dd/MM/yyyy")).Replace("{dueDate}",loan.DueDate.ToString("dd/MM/yyyy"))

                do!
                    task {
                        do! 
                            mailNotificator.SendEmailAsync(
                                fromEmail,
                                fromName,
                                userDetails.AppUser.Email,
                                mailBodyRetriever.GetLoanNotificationSubject shortLang,
                                emailBody
                            )
                        return Ok ()
                    }

                return result
            }

    member this.GetLoanAsync (context: UserContext, id: LoanId, ?ct: CancellationToken): TaskResult<Loan, string> = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result =
                    loanViewerAsync (Some ct) id.Value
                return result |> snd
            }

    member this.GetRefreshableLoanDetailsAsync (context: UserContext, loanId: LoanId, ?ct: CancellationToken): TaskResult<RefreshableLoanDetails, string> = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                            let! loan = 
                                loanViewerAsync (ct |> Some) loanId.Value |> TaskResult.map snd
                            let! book = 
                                bookViewerAsync (ct |> Some) loan.BookId.Value |> TaskResult.map snd
                            let! userDetail = 
                                usersService.GetUserDetailsAsync (context, loan.UserId, ct)
                            do!
                                book.TenantId = tenantId
                                |> Result.ofBool $"Book tenant id {book.TenantId} does not match user tenant id {tenantId}"

                            return
                                { 
                                    Loan = loan
                                    Book = book
                                    UserDetails = userDetail
                                }
                        }
                taskResult {
                    let! loanDetails = refresher (Some ct)
                    return
                        {
                            LoanDetails = loanDetails
                            Refresher = refresher
                        } :> RefreshableAsync<RefreshableLoanDetails>
                        ,
                        [
                            loanId.Value;
                            loanDetails.Book.Id;
                            loanDetails.UserDetails.User.Id
                        ]
                    }
        let key = DetailsCacheKey.OfType typeof<RefreshableLoanDetails> loanId.Value    
        StateView.getRefreshableDetailsTaskResultAsync<RefreshableLoanDetails> (fun ct -> detailsBuilder ct) key ct
    member this.GetLoansAsync (context: UserContext, ?ct: CancellationToken): TaskResult<List<Loan>, string> = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! result =
                    StateView.getAllAggregateStatesAsync<Loan, LoanEvent, string> eventStore (ct |> Some)
                    |> TaskResult.map (fun x -> x |> List.map snd)
                return 
                    result
                    |> List.filter (fun l -> l.TenantId = tenantId)
            }

    member this.ReleaseLoanAsync (context: UserContext, loanId: LoanId, shortLang: ShortLang,  dateTime: System.DateTime, ?ct: CancellationToken)= 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! loan = 
                    loanViewerAsync (Some ct) loanId.Value 
                    |> TaskResult.map snd
                let! book = 
                    bookViewerAsync (Some ct) loan.BookId.Value
                    |> TaskResult.map snd
                let! user =
                    userViewerAsync (Some ct) loan.UserId.Value
                    |> TaskResult.map snd
                let releaseLoanCommand = 
                    BookCommand.ReleaseLoan (loanId, dateTime)
                let releaseBookCommand =
                    LoanCommand.Return dateTime
                let userReleaseLoanCommandr = 
                    UserCommand.ReleaseLoan (loanId)
                let! userDetails = 
                    usersService.GetUserDetailsAsync (context, loan.UserId, ct)
                let! emailTextRetrieved =
                    mailBodyRetriever.GetReleaseLoanNotificationTextMailAsync(shortLang)

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

                let emailBody = emailTextRetrieved.Replace("{bookTitle}", book.Title.Value)
                do!
                    task
                        {
                            do!
                                mailNotificator.SendEmailAsync (
                                    fromEmail, 
                                    fromName, 
                                    userDetails.AppUser.Email, 
                                    mailBodyRetriever.GetReleaseLoanNotificationSubject shortLang, 
                                    emailBody
                                )
                            return Ok ()
                        }
                return result
            }

    member this.GetHistoryLoansOfUserAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! loans = 
                    StateView.getAllFilteredAggregateStatesAsync<Loan, LoanEvent, string> 
                        (fun loan -> loan.UserId = userId && loan.TenantId = tenantId)
                        eventStore
                        (ct |> Some)
                    |> TaskResult.map (fun x -> x |> List.map snd)
                return loans
            }

    member this.TransformReservationIntoLoanAsync (context: UserContext, reservationId: ReservationId, providedReservationCode: ReservationCode, shortLang: ShortLang, now: DateTime, ?ct: CancellationToken)= 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! reservation = 
                    reservationViewerAsync (Some ct) reservationId.Value
                    |> TaskResult.map snd
                let! reservationDetails =
                    reservationService.GetReservationDetailsAsync (context, reservationId, ct)

                let book =
                    reservationDetails.Book

                do!
                    book.NoLoan
                    |> Result.ofBool "Book is already loaned"

                let! matchReservationCode = 
                    reservation.ReservationCode = providedReservationCode
                    |> Result.ofBool "Reservation code must match"

                let makeReservationLoaned = 
                    ReservationCommand.Loan now

                let! loan = 
                    reservationDetails.ToLoan now

                let setBookLoaned =
                    BookCommand.SetCurrentLoanFromReservation (reservationId, loan.LoanId, now)

                let makeLoanFromReservation = 
                    UserCommand.LoanFromReservation (loan.LoanId, reservationId)

                let! emailTextRetrieved =
                    mailBodyRetriever.GetLoanNotificationTextMailAsync(shortLang)

                let! result = 
                    runInitAndThreeAggregateCommandsMdAsync<Reservation, ReservationEvent, Book, BookEvent, User, UserEvent, string, Loan>
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

                let emailBody = emailTextRetrieved.Replace("{bookTitle}",book.Title.Value).Replace("{loanedAt}", now.ToString("dd/MM/yyyy")).Replace("{dueDate}", loan.DueDate.ToString("dd/MM/yyyy"))

                do! 
                    mailNotificator.SendEmailAsync(
                        fromEmail,
                        fromName,
                        reservationDetails.UserDetails.AppUser.Email,
                        mailBodyRetriever.GetLoanNotificationSubject shortLang,
                        emailBody
                    )
                
                return result
            }

    new(eventStore: IEventStore<string>, reservationService: IReservationService, usersService: IUserService, mailNotificator: IMailNotificator, localizer: IStringLocalizer<SharedResources>, configuration: IConfiguration, mailBodyRetriever: IMailBodyRetriever, userTenantResolverService: IUserTenantResolverService) =
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
            userTenantResolverService,
            reservationService, 
            usersService, 
            mailNotificator, 
            configuration.GetValue<int>("BooksLibrary:MaxLoanPerUser", 3),
            configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com"),
            configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library"),
            localizer, 
            mailBodyRetriever
        )

    new(configuration: IConfiguration, reservationService: IReservationService, usersService: IUserService, mailNotificator: IMailNotificator, localizer: IStringLocalizer<SharedResources>, mailBodyRetriever: IMailBodyRetriever, secretsReader: SecretsReader, userTenantResolverService: IUserTenantResolverService) =
        LoanService(PgStorage.PgEventStore (secretsReader.GetBookLibraryConnectionString ()), reservationService, usersService, mailNotificator, localizer, configuration, mailBodyRetriever, userTenantResolverService)

    new(connectionString: string, reservationService: IReservationService, usersService: IUserService, mailNotificator: IMailNotificator, localizer: IStringLocalizer<SharedResources>, configuration: IConfiguration, mailBodyRetriever: IMailBodyRetriever, userTenantResolverService: IUserTenantResolverService) =
        LoanService(PgStorage.PgEventStore connectionString, reservationService, usersService, mailNotificator, localizer, configuration, mailBodyRetriever, userTenantResolverService)

    interface ILoanService with
        member this.AddLoanAsync (context: UserContext, loan: Loan, shortLang:ShortLang, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.AddLoanAsync (context, loan, shortLang, System.DateTime.Now, ct)
        member this.GetLoanAsync (context: UserContext, id: LoanId, ?ct: CancellationToken) =  
            let ct = defaultArg ct CancellationToken.None
            this.GetLoanAsync (context, id, ct)
        member this.ReleaseLoanAsync (context: UserContext, loanId: LoanId, shortLang: ShortLang, now: DateTime, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.ReleaseLoanAsync (context, loanId, shortLang, now, ct)
        member this.GetLoansAsync (context: UserContext, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetLoansAsync (context, ct)
        member this.GetHistoryLoansOfUserAsync (context: UserContext, userId: UserId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetHistoryLoansOfUserAsync (context, userId, ct)
        member this.TransformReservationIntoLoanAsync (context: UserContext, reservationId: ReservationId, providedReservationCode: ReservationCode, shortLang: ShortLang, now: DateTime, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.TransformReservationIntoLoanAsync (context, reservationId, providedReservationCode, shortLang, now, ct)