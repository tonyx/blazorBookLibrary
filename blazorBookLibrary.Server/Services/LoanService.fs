
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

type LoanService 
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        userViewerAsync: AggregateViewerAsync2<User>
    ) =
    new (eventStore: IEventStore<string>)
        =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        LoanService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync
        )    
    new (configuration: Microsoft.Extensions.Configuration.IConfiguration) 
        =
        let connectionString = configuration.Item("ConnectionStrings::BookLibraryDbConnection")
        let eventStore = PgStorage.PgEventStore connectionString
        LoanService(eventStore)

    new (connectionString: string)
        =
        let eventStore = PgStorage.PgEventStore connectionString
        LoanService(eventStore)

    member this.AddLoanAsync (loan: Loan, dateTime: System.DateTime, ?ct: CancellationToken)= 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! book = 
                    bookViewerAsync (Some ct) loan.BookId.Value 
                    |> TaskResult.map snd

                let! user =
                    userViewerAsync (Some ct) loan.UserId.Value
                    |> TaskResult.map snd
                
                let setCurrentLoanCommand = 
                    BookCommand.SetCurrentLoan (loan.LoanId, dateTime)

                let addLoanToUser =     
                    UserCommand.AddLoan (loan.LoanId)

                let! result = 
                    runInitAndTwoAggregateCommands<Book, BookEvent, User, UserEvent, string, Loan>
                        book.Id
                        user.Id
                        eventStore
                        messageSenders
                        loan
                        setCurrentLoanCommand
                        addLoanToUser
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
    member this.ReleaseLoanAsync (loanId: LoanId, dateTime: System.DateTime, ?ct: CancellationToken)= 
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
                let! result = 
                    runThreeNAggregateCommands<Book, BookEvent, Loan, LoanEvent, User, UserEvent, string>
                        [book.Id]
                        [loan.Id]
                        [user.Id]
                        eventStore
                        messageSenders
                        [releaseLoanCommand]
                        [releaseBookCommand]
                        [userReleaseLoanCommandr]
                return result
            }

    interface ILoanService with
        member this.AddLoanAsync (loan: Loan, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.AddLoanAsync (loan, System.DateTime.Now, ct)
        member this.GetLoanAsync (id: LoanId, ?ct: CancellationToken) =  
            let ct = defaultArg ct CancellationToken.None
            this.GetLoanAsync (id, ct)
        member this.ReleaseLoanAsync (loanId: LoanId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            this.ReleaseLoanAsync (loanId, System.DateTime.Now, ct)