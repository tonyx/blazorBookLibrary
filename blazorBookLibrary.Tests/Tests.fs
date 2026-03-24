module Tests

open System
open Expecto
open DotNetEnv
open Sharpino.PgStorage
open BookLibrary.Domain
open Sharpino.Cache
open Sharpino.Core
open BookLibrary.Shared.Commons
open Sharpino.CommandHandler
open Sharpino.EventBroker
open BookLibrary.Application.ServiceLayer
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open System.Threading

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

let getBookServiceLayer = 
    fun _ -> 
        BookLibraryServiceLayer
            (pgEventStore, 
            MessageSenders.NoSender, 
            bookViewerAsync, 
            authorViewerAsync, 
            editorViewerAsync, 
            reservationViewerAsync, 
            loanViewerAsync)

[<Tests>]
let tests =
  testList "samples" [
    testCase "create and retrieve an author" <| fun _ ->
        setUp()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"
        let getAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk getAuthor "should be ok"

    testCase "create and retrieve a book with no authors" <| fun _ ->
        setUp()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "The Great Gatsby") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())

        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"

        let retrieveBook =
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

    testCase "create book with a valid author - Ok" <| fun _ ->
        setUp()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"
        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())

        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"

        let retrieveBook =
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

    testCase "creating a book with an unexisting Author must be an Error" <| fun _ ->
        setUp()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "The Great Gatsby") [AuthorId.New()] [] [] None (Year.New 1924) (Isbn.NewEmpty())

        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isError addBook "should be error"

    testCase "the author contains reference to the books they are author of - Ok " <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"
        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

    testCase "the author contains reference to the books they are author of async - Ok " <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())

        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"

        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

    testCase "the author contains references to the books they are author of, test many authors - Ok " <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let author2 = Author.NewWithoutIsni (Name.New "Murakami")
        let addAuthor2 = 
            bookServiceLayer.AddAuthorAsync author2
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor2 "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId; author2.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"
        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

        let retrieveAuthor2 = 
            bookServiceLayer.GetAuthorAsync author2.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor2 "should be ok"

        let (authorRetrieved2: Author) = retrieveAuthor2 |> Result.get
        Expect.isTrue (authorRetrieved2.Books |> List.contains book.BookId) "should contain the book"
        
    testCase "add an author, add a book without authors and then add an author to that book and retrieve the author checking the mutal references  - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"
        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let addAuthorToBook = 
            bookServiceLayer.AddAuthorToBookAsync(author.AuthorId, book.BookId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthorToBook "should be ok"

        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

    /// XXX
    testCase "add an author, add a book without authors and then add an author to that book and retrieve the author checking the mutal references async - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())

        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"
        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let addAuthorToBook = 
            bookServiceLayer.AddAuthorToBookAsync(author.AuthorId, book.BookId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addAuthorToBook "should be ok"

        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

    testCase "add a book with an author, check the mutal references, then remove the author from that book and check the mutal references are removed - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"

        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

        let removeAuthorFromBook = 
            bookServiceLayer.RemoveAuthorFromBookAsync (author.AuthorId, book.BookId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk removeAuthorFromBook "should be ok"

        let retrieveAuthor = 
            bookServiceLayer.GetAuthorAsync author.AuthorId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveAuthor "should be ok"

        let (authorRetrieved: Author) = retrieveAuthor |> Result.get
        Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId |> not) "should not contain the book"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId |> not) "should not contain the author"

    testCase "create a book and then attach a reservation to it - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveBook "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

        let userId = UserId.New ()
        let timeSlot = TimeSlot.New (System.DateTime.Now.AddHours(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

        let reservation = Reservation.New book.BookId userId timeSlot System.DateTime.Now
        let addReservation = 
            bookServiceLayer.AddReservationAsync(reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addReservation "should be ok"

        let retrieveReservation = 
            bookServiceLayer.GetReservationAsync (reservation.ReservationId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveReservation "should be ok"

        let retrieveBook =
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.CurrentReservations |> List.contains reservation.ReservationId) "should contain the reservation"

    testCase "a reservation cannot be added if the timeslot is in the past - Error" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let author = Author.NewWithoutIsni (Name.New "John Doe")
        let addAuthor = 
            bookServiceLayer.AddAuthorAsync author
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addAuthor "should be ok"

        let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let userId = UserId.New ()
        let timeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(-1)) System.DateTime.Now

        let reservation = Reservation.New book.BookId userId timeSlot System.DateTime.Now
        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isError addReservation "should be error"

    testCase "if a book has no reservations then you can loan it - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync loan.LoanId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

    testCase "a book that has a loan in progress cannot be loaned again - Error" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync loan.LoanId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        let timeSlot2 = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan2 = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot2

        let addLoan2 = 
            bookServiceLayer.AddLoanAsync (loan2, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isError addLoan2 "should be error"

    testCase "a book that has a loan in progress cannot be loaned again async - Error" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync (loan.LoanId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        let timeSlot2 = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan2 = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot2

        let addLoan2 = 
            bookServiceLayer.AddLoanAsync (loan2, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isError addLoan2 "should be error"

    testCase "loan a book and then release the loan, the book then has no loan and is returned at something - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync loan.LoanId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync loan.LoanId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

    testCase "loan a book and then release the loan, the book then has no loan and is returned at something, use async - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync loan.LoanId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk releaseLoan "should be ok"

        let retrieveLoan = 
            bookServiceLayer.GetLoanAsync loan.LoanId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveLoan "should be ok"

        let bookRetrieved = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved "should be ok"

        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

        let (loanRetrieved: Loan) = retrieveLoan |> Result.get
        Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

    testCase "should be able to get the book details containing the loan and the reservations, which are empty for fresh book - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

    testCase "should be able to get the book details containing the loan and the reservations, which are empty for fresh book async - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync (book, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) (System.DateTime.Now) timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let (bookRetrieved: Book) = retrieveBook |> Result.get
        Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async (book.BookId)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

    testCase "add an overlapping reservation and verify it will be an error - Error" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync (book, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

        let reservation = Reservation.New book.BookId (UserId.New ()) timeSlot System.DateTime.UtcNow

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let overlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddDays(5)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let overlappingReservation = Reservation.New book.BookId (UserId.New ()) overlappingTimeSlot System.DateTime.UtcNow

        let addOverlappingReservation = 
            bookServiceLayer.AddReservationAsync (overlappingReservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isError addOverlappingReservation "should be an error"
        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"
        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.equal bookDetail.FutureReservations.Length 1 "should contain one reservation"

    testCase "add a non overlapping reservation and verify it will be ok, expect then two reservation on that book - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync (book, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

        let reservation = Reservation.New book.BookId (UserId.New ()) timeSlot System.DateTime.UtcNow

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let nonOverlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddDays((float)timeSlotDurationInDays + 1.0)) (System.DateTime.Now.AddDays( 2.0 * (float)timeSlotDurationInDays + 1.0))
        let nonOverlappingReservation = Reservation.New book.BookId (UserId.New ()) nonOverlappingTimeSlot System.DateTime.UtcNow

        let addNonOverlappingReservation = 
            bookServiceLayer.AddReservationAsync (nonOverlappingReservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addNonOverlappingReservation "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.equal bookDetail.FutureReservations.Length 2 "should contain two reservations"

    testCase "add and remove a reservation async - Ok " <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync (book, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

        let reservation = Reservation.New book.BookId (UserId.New ()) timeSlot System.DateTime.UtcNow

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"
        let bookRetrieved = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved "should be ok"
        let (bookRetrieved: Book) = bookRetrieved |> Result.get
        Expect.equal (bookRetrieved.CurrentReservations |> List.length) 1 "should contain one reservation"

        let removeReservation = 
            bookServiceLayer.RemoveReservationAsync (reservation.ReservationId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk removeReservation "should be ok"

        let retrieveReservation = 
            bookServiceLayer.GetReservationAsync (reservation.ReservationId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isError retrieveReservation "should not be ok"

        let bookRetrieved2 = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookRetrieved2 "should be ok"
        let (bookRetrieved2: Book) = bookRetrieved2 |> Result.get
        Expect.equal (bookRetrieved2.CurrentReservations |> List.length) 0 "should not contain reservations"

    testCase "verify that when the loan is released then the details are always in sync - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

    testCase "verify that when the loan is released then the details are always in sync 2 - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

    testCase "verify that when the loan is released then the details are always in sync - async Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        // let addLoan = bookServiceLayer.AddLoan loan System.DateTime.Now
        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId 
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        // let releaseLoan = bookServiceLayer.ReleaseLoan loan.LoanId System.DateTime.Now
        // Expect.isOk releaseLoan "should be ok"
        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId 
            |> Async.AwaitTask
            |> Async.RunSynchronously
            
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

    testCase "verify that when the loan is released then the details are always in sync 2 - async Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        // let addLoan = bookServiceLayer.AddLoan loan System.DateTime.Now
        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId 
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        // let releaseLoan = bookServiceLayer.ReleaseLoan loan.LoanId System.DateTime.Now
        // Expect.isOk releaseLoan "should be ok"
        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId 
            |> Async.AwaitTask
            |> Async.RunSynchronously
            
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

    testCase "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId 
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

        let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let reservation = Reservation.New book.BookId (UserId.New ()) futureTimeSlot (System.DateTime.Now)

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let bookDetail3 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail3 "should be ok"

        let (bookDetail3: BookDetails2) = bookDetail3 |> Result.get
        Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.FutureReservations |> List.length = 1) "should contain the reservation"

    testCase "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation 2" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId 
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

        let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let reservation = Reservation.New book.BookId (UserId.New ()) futureTimeSlot (System.DateTime.Now)

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let bookDetail3 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail3 "should be ok"

        let (bookDetail3: BookDetails2) = bookDetail3 |> Result.get
        Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.FutureReservations |> List.length = 1) "should contain the reservation"

    testCase "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation async" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

        let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let reservation = Reservation.New book.BookId (UserId.New ()) futureTimeSlot (System.DateTime.Now)

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let bookDetail3 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk bookDetail3 "should be ok"

        let (bookDetail3: BookDetails2) = bookDetail3 |> Result.get
        Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.FutureReservations |> List.length = 1) "should contain the reservation"
        
    testCase "should be able to add more than one reservation to a book and then retrieve the bookdetails containing the reservations" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"


        let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let reservation = Reservation.New book.BookId (UserId.New ()) futureTimeSlot (System.DateTime.Now)

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let bookDetail3 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail3 "should be ok"

        let (bookDetail3: BookDetails2) = bookDetail3 |> Result.get
        Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.FutureReservations |> List.length = 1) "should contain the reservation"

        let secondFutureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(2)) (System.DateTime.Now.AddMonths(3))
        let secondReservation = Reservation.New book.BookId (UserId.New ()) secondFutureTimeSlot (System.DateTime.Now)

        let addSecondReservation = 
            bookServiceLayer.AddReservationAsync (secondReservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addSecondReservation "should be ok"

        let bookDetail4 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail4 "should be ok"

        let (bookDetail4: BookDetails2) = bookDetail4 |> Result.get
        Expect.isTrue (bookDetail4.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail4.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail4.FutureReservations |> List.length = 2) "should contain the reservation"

    testCase "should be able to add more than one reservation to a book and then retrieve the bookdetails containing the reservations async - Ok" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync (book, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let retrieveBook = 
            bookServiceLayer.GetBookAsync (book.BookId, CancellationToken.None)
            |> Async.AwaitTask
            |> Async.RunSynchronously

        Expect.isOk retrieveBook "should be ok"

        let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
        let loan = Loan.New book.BookId (UserId.New ()) System.DateTime.Now timeSlot

        let addLoan = 
            bookServiceLayer.AddLoanAsync (loan, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addLoan "should be ok"


        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let releaseLoan = 
            bookServiceLayer.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk releaseLoan "should be ok"

        let bookDetail2 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail2 "should be ok"

        let (bookDetail2: BookDetails2) = bookDetail2 |> Result.get
        Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail2.FutureReservations |> List.isEmpty) "should not contain reservations"

        let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let reservation = Reservation.New book.BookId (UserId.New ()) futureTimeSlot (System.DateTime.Now)

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let bookDetail3 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail3 "should be ok"

        let (bookDetail3: BookDetails2) = bookDetail3 |> Result.get
        Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.FutureReservations |> List.length = 1) "should contain the reservation"

        let secondFutureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(2)) (System.DateTime.Now.AddMonths(3))
        let secondReservation = Reservation.New book.BookId (UserId.New ()) secondFutureTimeSlot (System.DateTime.Now)

        let addSecondReservation = 
            bookServiceLayer.AddReservationAsync (secondReservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addSecondReservation "should be ok"

        let bookDetail4 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail4 "should be ok"

        let (bookDetail4: BookDetails2) = bookDetail4 |> Result.get
        Expect.isTrue (bookDetail4.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail4.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail4.FutureReservations |> List.length = 2) "should contain the reservation"

    testCase "cannot add a reservation that overlaps an existing reservation " <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let reservation = Reservation.New book.BookId (UserId.New ()) futureTimeSlot (System.DateTime.Now)

        let addReservation = 
            bookServiceLayer.AddReservationAsync (reservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addReservation "should be ok"

        let bookDetail3 = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail3 "should be ok"

        let (bookDetail3: BookDetails2) = bookDetail3 |> Result.get
        Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail3.FutureReservations |> List.length = 1) "should contain the reservation"

        let overlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
        let overlappingReservation = Reservation.New book.BookId (UserId.New ()) overlappingTimeSlot (System.DateTime.Now)

        let addOverlappingReservation = 
            bookServiceLayer.AddReservationAsync (overlappingReservation, System.DateTime.Now)
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isError addOverlappingReservation "should not be ok"

    testCase "when a there is no loan and no reservation the bookDetails should return a timeSlot that starts from now and ends in 30 days" <| fun _ ->
        setUp ()
        let bookServiceLayer = getBookServiceLayer()
        let book = Book.New (Title.New "the constitution") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
        let addBook = 
            bookServiceLayer.AddBookAsync book
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk addBook "should be ok"

        let bookDetail = 
            bookServiceLayer.GetBookDetails2Async book.BookId
            |> Async.AwaitTask
            |> Async.RunSynchronously
        Expect.isOk bookDetail "should be ok"

        let (bookDetail: BookDetails2) = bookDetail |> Result.get
        Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail.CurrentLoan |> Option.isNone) "should not contain the loan"
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let timeNow = System.DateTime.Now

        let expectedTimeSlot = TimeSlot.New (timeNow) (timeNow.AddDays(timeSlotDurationInDays))
        Expect.isTrue (bookDetail.FutureReservations |> List.isEmpty) "should not contain reservations"

        let actualSuggestedTimeSlot =
            bookDetail.GetNextAvailableTimeSlot(timeSlotDurationInDays, timeNow)

        Expect.equal actualSuggestedTimeSlot expectedTimeSlot "should return a timeSlot that starts from now and ends in 30 days"

  ]
  |> testSequenced
