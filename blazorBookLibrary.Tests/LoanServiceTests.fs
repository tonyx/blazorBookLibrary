module LoanServiceTests

open System
open Expecto
open DotNetEnv
open Sharpino.PgStorage
open BookLibrary.Domain
open BookLibrary.Services
open BookLibrary.Shared.Details
open Sharpino.Cache
open Sharpino.Core
open BookLibrary.Shared.Commons
open Sharpino.CommandHandler
open Sharpino.EventBroker
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open System.Threading
open BookLibrary.Services

Env.Load() |> ignore
let password = Environment.GetEnvironmentVariable("password")

let connection =
    "Server=127.0.0.1;"+
    "Database=sharpino_booklibrary_test;" +
    "User Id=safe;"+
    $"Password={password}"

let pgEventStore:Sharpino.Storage.IEventStore<string> = PgEventStore connection
let setUp () =
    pgEventStore.Reset Book.Version Book.StorageName
    pgEventStore.ResetAggregateStream Book.Version Book.StorageName
    pgEventStore.Reset Author.Version Author.StorageName
    pgEventStore.ResetAggregateStream Author.Version Author.StorageName
    pgEventStore.Reset Editor.Version Editor.StorageName
    pgEventStore.ResetAggregateStream Editor.Version Editor.StorageName
    pgEventStore.Reset Reservation.Version Reservation.StorageName
    pgEventStore.ResetAggregateStream Reservation.Version Reservation.StorageName
    pgEventStore.Reset Loan.Version Loan.StorageName
    pgEventStore.ResetAggregateStream Loan.Version Loan.StorageName
    pgEventStore.Reset User.Version User.StorageName
    pgEventStore.ResetAggregateStream User.Version User.StorageName
    AggregateCache3.Instance.Clear()            

let timeSlotDurationInDays =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appSettings.json")
            .Build()
    config.GetValue<int>("TimeSlotLoanDurationInDays", 30)

let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> pgEventStore
let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> pgEventStore
let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> pgEventStore
let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> pgEventStore
let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> pgEventStore
let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> pgEventStore

let getAuthorService = 
    fun () -> 
        AuthorService(
            pgEventStore, 
            MessageSenders.NoSender, 
            bookViewerAsync, 
            authorViewerAsync, 
            editorViewerAsync, 
            reservationViewerAsync, 
            loanViewerAsync)

let getBookService = 
    fun _ -> 
        BookService
            (pgEventStore, 
            MessageSenders.NoSender, 
            bookViewerAsync, 
            authorViewerAsync, 
            editorViewerAsync, 
            reservationViewerAsync, 
            loanViewerAsync)
let getReservationService =
    fun _ ->
        ReservationService
            (pgEventStore, 
            MessageSenders.NoSender, 
            bookViewerAsync, 
            authorViewerAsync, 
            editorViewerAsync, 
            reservationViewerAsync, 
            loanViewerAsync)

let getLoanService =
    fun _ ->
        LoanService
            (pgEventStore, 
            MessageSenders.NoSender, 
            bookViewerAsync, 
            authorViewerAsync, 
            editorViewerAsync, 
            reservationViewerAsync, 
            loanViewerAsync,
            userViewerAsync)
let getUserService =
    fun _ ->
        UserService(
            pgEventStore,
            MessageSenders.NoSender,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync)

[<Tests>]
let tests =
    testList "loan service tests" [
        testCase "loan a book and then release the loan, the book then has no loan and is returned at something, use async - Ok" <| fun _ ->
            setUp ()
            let loanService = getLoanService()
            let bookService = getBookService()
            let userService = getUserService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let userId = UserId.New ()
            let user = User.New userId
            let addUser = 
                userService.CreateUserAsync user
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addUser "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let retrieveLoan = 
                loanService.GetLoanAsync loan.LoanId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveLoan "should be ok"

            let bookRetrieved = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

            let releaseLoan = 
                loanService.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk releaseLoan "should be ok"

            let retrieveLoan = 
                loanService.GetLoanAsync loan.LoanId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveLoan "should be ok"

            let bookRetrieved = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let userRetrieved = 
                userService.GetUserAsync userId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk userRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        testCase "loan a book and verify that the user has that loan - Ok" <| fun _ ->
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let userId = UserId.New ()
            let user = User.New userId
            let addUser = 
                userService.CreateUserAsync user
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addUser "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let userRetrieved = 
                userService.GetUserAsync userId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk userRetrieved "should be ok"

            let (userRetrieved: User) = userRetrieved |> Result.get
            Expect.isTrue (userRetrieved.CurrentLoans |> List.contains loan.LoanId) "should contain the loan"

        testCase "loan a book and then release it. Verify that the book and the user don't relate to the loan anymore - Ok" <| fun _ ->
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let userId = UserId.New ()
            let user = User.New userId
            let addUser = 
                userService.CreateUserAsync user
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addUser "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let releaseLoan = 
                loanService.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk releaseLoan "should be ok"

            let bookRetrieved = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let userRetrieved = 
                userService.GetUserAsync userId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk userRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (userRetrieved: User) = userRetrieved |> Result.get
            Expect.isFalse (userRetrieved.CurrentLoans |> List.contains loan.LoanId) "should not contain the loan"
            
    ]