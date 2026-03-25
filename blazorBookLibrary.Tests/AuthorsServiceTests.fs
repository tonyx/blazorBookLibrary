
module AuthorsServiceTests

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

let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> pgEventStore
let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> pgEventStore
let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> pgEventStore
let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> pgEventStore
let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> pgEventStore

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
        BookLibraryService
            (pgEventStore, 
            MessageSenders.NoSender, 
            bookViewerAsync, 
            authorViewerAsync, 
            editorViewerAsync, 
            reservationViewerAsync, 
            loanViewerAsync)

let timeSlotDurationInDays =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appSettings.json")
            .Build()
    config.GetValue<int>("TimeSlotLoanDurationInDays", 30)


[<Tests>]
let tests =
    testList "authors service" [
        testCase "create and retrieve an author" <| fun _ ->
            setUp()
            let authorService = getAuthorService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")

            let addAuthor = 
                authorService.AddAuthorAsync author
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor "should be ok"
            let getAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk getAuthor "should be ok"

        testCase "the author contains reference to the book they are author of " <| fun _ ->
            setUp()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let addAuthor = 
                authorService.AddAuthorAsync author
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let getAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously

            let (author: Author ) = getAuthor |> Result.get
            Expect.isTrue (List.contains book.BookId author.Books) "should contain the book"

        testCase "the author contains references to the books they are author of, test many authors - Ok " <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let addAuthor = 
                authorService.AddAuthorAsync author
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor "should be ok"

            let author2 = Author.NewWithoutIsni (Name.New "Murakami")
            let addAuthor2 = 
                authorService.AddAuthorAsync author2
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor2 "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId; author2.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let retrieveAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveAuthor "should be ok"

            let (authorRetrieved: Author) = retrieveAuthor |> Result.get
            Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

            let retrieveAuthor2 = 
                authorService.GetAuthorAsync author2.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveAuthor2 "should be ok"

            let (authorRetrieved2: Author) = retrieveAuthor2 |> Result.get
            Expect.isTrue (authorRetrieved2.Books |> List.contains book.BookId) "should contain the book"

        testCase "add an author, add a book without authors and then add an author to that book and retrieve the author checking the mutal references  - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let addAuthor = 
                authorService.AddAuthorAsync author
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let retrieveAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveAuthor "should be ok"

            let addAuthorToBook = 
                bookService.AddAuthorToBookAsync(author.AuthorId, book.BookId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthorToBook "should be ok"

            let retrieveAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveAuthor "should be ok"

            let (authorRetrieved: Author) = retrieveAuthor |> Result.get
            Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

        testCase "add an author, add a book without authors and then add an author to that book and retrieve the author checking the mutal references async - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let addAuthor = 
                authorService.AddAuthorAsync author
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [] [] [] None (Year.New 1924) (Isbn.NewEmpty())

            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk addBook "should be ok"
            let retrieveAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveAuthor "should be ok"

            let addAuthorToBook = 
                bookService.AddAuthorToBookAsync(author.AuthorId, book.BookId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk addAuthorToBook "should be ok"

            let retrieveAuthor = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveAuthor "should be ok"

            let (authorRetrieved: Author) = retrieveAuthor |> Result.get
            Expect.isTrue (authorRetrieved.Books |> List.contains book.BookId) "should contain the book"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"
    ]
    |> testSequenced
