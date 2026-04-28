
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
        reservationService: IReservationService,
        usersService: IUserService,
        mailNotificator: IMailNotificator,
        maxLoanPerUser: int,
        fromEmail: string,
        fromName: string,
        localizer: IStringLocalizer<SharedResources>,
        mailBodyRetriever: IMailBodyRetriever

    ) =
    new 
        (eventStore: IEventStore<string>, 
        reservationService: IReservationService, 
        usersService: IUserService, 
        mailNotificator: IMailNotificator, 
        localizer: IStringLocalizer<SharedResources>, 
        configuration: IConfiguration,
        mailBodyRetriever: IMailBodyRetriever)
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let maxLoanPerUser = configuration.GetValue<int>("BooksLibrary:MaxLoanPerUser", 3)
        let fromEmail = configuration.GetValue<string>("BooksLibrary:FromEmail", "noreply@blazorbooklibrary.com")
        let fromName = configuration.GetValue<string>("BooksLibrary:FromName", "Blazor Book Library")

        LoanService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            reservationService,
            usersService,
            mailNotificator,
            maxLoanPerUser,
            fromEmail,
            fromName,
            localizer,
            mailBodyRetriever
        )

    new 
        (configuration: IConfiguration, 
        reservationService: IReservationService,
        usersService: IUserService, 
        mailNotificator: IMailNotificator, 
        localizer: IStringLocalizer<SharedResources>,
        mailBodyRetriever: IMailBodyRetriever,
        secretsReader: SecretsReader) 
        =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        LoanService(eventStore, reservationService, usersService, mailNotificator, localizer, configuration, mailBodyRetriever)

    new 
        (connectionString: string, 
        reservationService: IReservationService, 
        usersService: IUserService, 
        mailNotificator: IMailNotificator, 
        localizer: IStringLocalizer<SharedResources>, 
        configuration: IConfiguration,
        mailBodyRetriever: IMailBodyRetriever)
        =
        let eventStore = PgStorage.PgEventStore connectionString
        LoanService(eventStore, reservationService, usersService, mailNotificator, localizer, configuration, mailBodyRetriever)

    member this.AddLoanAsync (loan: Loan, shortLang: ShortLang, dateTime: System.DateTime, ?ct: CancellationToken)= 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) loan.BookId.Value 
                    |> TaskResult.map snd

                let! user =
                    userViewerAsync (Some ct) loan.UserId.Value
                    |> TaskResult.map snd
                
                let! userDetails = 
                    usersService.GetUserDetailsAsync (user.UserId, ct)
                
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

    member this.GetLoanAsync (id: LoanId, ?ct: CancellationToken): TaskResult<Loan, string> = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result =
                    loanViewerAsync (Some ct) id.Value
                return result |> snd
            }

    member this.GetRefreshableLoanDetailsAsync (loanId: LoanId, ?ct: CancellationToken): TaskResult<RefreshableLoanDetails, string> = 
        let detailsBuilder =
            fun (ct: Option<CancellationToken>) ->
                let ct = ct |> Option.defaultValue CancellationToken.None
                let refresher =
                    fun (ct: Option<CancellationToken>) ->
                        taskResult {
                            let ct = ct |> Option.defaultValue CancellationToken.None
                            let! loan = 
                                loanViewerAsync (ct |> Some) loanId.Value |> TaskResult.map snd
                            let! book = 
                                bookViewerAsync (ct |> Some) loan.BookId.Value |> TaskResult.map snd
                            let! userDetail = 
                                usersService.GetUserDetailsAsync (loan.UserId, ct)
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
    member this.GetLoansAsync (?ct: CancellationToken): TaskResult<List<Loan>, string> = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! result =
                    StateView.getAllAggregateStatesAsync<Loan, LoanEvent, string> eventStore (ct |> Some)
                    |> TaskResult.map (fun x -> x |> List.map snd)
                return result
            }

    member this.ReleaseLoanAsync (loanId: LoanId, shortLang: ShortLang,  dateTime: System.DateTime, ?ct: CancellationToken)= 
        taskResult
            {
                printf "XXXXX 300 - Releasing loan %A\n" loanId
                let ct = defaultArg ct CancellationToken.None
                let! loan = 
                    loanViewerAsync (Some ct) loanId.Value 
                    |> TaskResult.map snd
                printf "XXXXX 301 - Releasing loan %A\n" loan
                let! book = 
                    bookViewerAsync (Some ct) loan.BookId.Value
                    |> TaskResult.map snd
                printf "XXXXX 302 - Releasing loan %A\n" book
                let! user =
                    userViewerAsync (Some ct) loan.UserId.Value
                    |> TaskResult.map snd
                printf "XXXXX 303 - Releasing loan %A\n" user
                let releaseLoanCommand = 
                    BookCommand.ReleaseLoan (loanId, dateTime)
                printf "XXXXX 304 - Releasing loan %A\n" releaseLoanCommand
                let releaseBookCommand =
                    LoanCommand.Return dateTime
                printf "XXXXX 305 - Releasing loan %A\n" releaseBookCommand
                let userReleaseLoanCommandr = 
                    UserCommand.ReleaseLoan (loanId)
                printf "XXXXX 306 - Releasing loan %A\n" userReleaseLoanCommandr
                let! userDetails = 
                    usersService.GetUserDetailsAsync loan.UserId
                printf "XXXXX 307 - Releasing loan %A\n" userDetails
                let! emailTextRetrieved =
                    mailBodyRetriever.GetReleaseLoanNotificationTextMailAsync(shortLang)
                printf "XXXXX 308 - Releasing loan %A\n" emailTextRetrieved

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
                printf "XXXXX 309 - Releasing loan %A\n" result

                let emailBody = emailTextRetrieved.Replace("{bookTitle}", book.Title.Value)
                printf "XXXXX 310 - Releasing loan %A\n" emailBody
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
    member this.GetHistoryLoansOfUserAsync (userId: UserId, ?ct: CancellationToken) = 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! loans = 
                    StateView.getAllFilteredAggregateStatesAsync<Loan, LoanEvent, string> 
                        (fun loan -> loan.UserId = userId)
                        eventStore
                        (ct |> Some)
                    |> TaskResult.map (fun x -> x |> List.map snd)
                return loans
            }

    member this.TransformReservationIntoLoanAsync (reservationId: ReservationId, providedReservationCode: ReservationCode, shortLang: ShortLang, now: DateTime, ?ct: CancellationToken)= 
        let ct = defaultArg ct CancellationToken.None
        taskResult
            {
                let! reservation = 
                    reservationViewerAsync (Some ct) reservationId.Value
                    |> TaskResult.map snd
                let! reservationDetails =
                    reservationService.GetReservationDetailsAsync (reservationId, ct)

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

    interface ILoanService with
        member this.AddLoanAsync (loan: Loan, shortLang:ShortLang, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.AddLoanAsync (loan, shortLang, System.DateTime.Now, ct)
        member this.GetLoanAsync (id: LoanId, ?ct: CancellationToken) =  
            let ct = defaultArg ct CancellationToken.None
            this.GetLoanAsync (id, ct)
        member this.ReleaseLoanAsync (loanId: LoanId, shortLang: ShortLang, now: DateTime, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.ReleaseLoanAsync (loanId, shortLang, now, ct)
        member this.GetLoansAsync (?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetLoansAsync ct
        member this.GetHistoryLoansOfUserAsync (userId: UserId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.GetHistoryLoansOfUserAsync (userId, ct)
        member this.TransformReservationIntoLoanAsync (reservationId: ReservationId, providedReservationCode: ReservationCode, shortLang: ShortLang, now: DateTime, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.TransformReservationIntoLoanAsync (reservationId, providedReservationCode, shortLang, now, ct)