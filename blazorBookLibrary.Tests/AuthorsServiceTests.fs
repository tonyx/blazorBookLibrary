
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
// open BookLibrary.Application.ServiceLayer
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open System.Threading
open BookLibrary.Shared.Services


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
            loanViewerAsync)

let timeSlotDurationInDays =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appsettings.json", true)
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

        testCase "add two authors and retrieve them all - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let author1 = Author.NewWithoutIsni (Name.New "Author One")
            let author2 = Author.NewWithoutIsni (Name.New "Author Two")
            
            let addAuthor1 = 
                authorService.AddAuthorAsync author1
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor1 "should be ok"
            
            let addAuthor2 = 
                authorService.AddAuthorAsync author2
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor2 "should be ok"
            
            let getAllResult = 
                (authorService :> IAuthorService).GetAllAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously

            
            Expect.isOk getAllResult "should be ok"
            let allAuthors = getAllResult |> Result.get
            Expect.equal allAuthors.Length 2 "should have 2 authors"
            Expect.isTrue (allAuthors |> List.exists (fun a -> a.AuthorId = author1.AuthorId)) "should contain author 1"
            Expect.isTrue (allAuthors |> List.exists (fun a -> a.AuthorId = author2.AuthorId)) "should contain author 2"

        testCase "add multiple authors - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let authors = 
                [
                    Author.NewWithoutIsni (Name.New "Author A")
                    Author.NewWithoutIsni (Name.New "Author B")
                    Author.NewWithoutIsni (Name.New "Author C")
                ]
            
            let addAuthorsResult = 
                (authorService :> IAuthorService).AddAuthorsAsync (authors)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isOk addAuthorsResult "should be ok"
            
            let getAllResult = 
                (authorService :> IAuthorService).GetAllAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isOk getAllResult "should be ok"
            let all = getAllResult |> Result.get
            Expect.equal all.Length 3 "should have 3 authors"

        testCase "filtering authors by name - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let name1 = Name.New "John Smith"
            let name2 = Name.New "Jane Doe"
            let author1 = Author.NewWithoutIsni name1
            let author2 = Author.NewWithoutIsni name2
            
            (authorService :> IAuthorService).AddAuthorsAsync [author1; author2] |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (authorService :> IAuthorService).SearchByNameAsync name1 |> Async.AwaitTask |> Async.RunSynchronously |> Result.get

            Expect.equal filtered.Length 1 "should have 1 author"
            Expect.equal filtered.[0].AuthorId author1.AuthorId "should be author 1"

        testCase "filtering authors by isni - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let isni1 = Isni.New "0000 0001 2103 2683" |> Result.get
            let isni2 = Isni.New "0000 0001 2103 2691" |> Result.get
            let author1 = Author.New (Name.New "Austen") isni1
            let author2 = Author.New (Name.New "Shakespeare") isni2
            
            (authorService :> IAuthorService).AddAuthorsAsync [author1; author2] |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (authorService :> IAuthorService).SearchByIsniAsync isni1 |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 author"
            Expect.equal filtered.[0].Isni isni1 "should have correct isni"

        testCase "filtering authors by isni and name - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let name = Name.New "John Smith"
            let name2 = Name.New "John Doe"
            let isni = Isni.New "0000 0001 2103 2683" |> Result.get
            let author1 = Author.New name isni
            let author2 = Author.New name2 (Isni.New "0000 0001 2103 2691" |> Result.get)
            
            (authorService :> IAuthorService).AddAuthorsAsync [author1; author2] |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (authorService :> IAuthorService).SearchByIsniAndNameAsync (isni, name) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 author"
            Expect.equal filtered.[0].AuthorId author1.AuthorId "should be author 1"

        testCase "add an author and then remove it - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            
            authorService.AddAuthorAsync author |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let removeResult = 
                (authorService :> IAuthorService).RemoveAsync author.AuthorId

                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isOk removeResult "should be ok"
            
            let getResult = 
                authorService.GetAuthorAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isError getResult "should be error as it's removed"

        testCase "cannot remove an author that has books - Error" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()
            let author = Author.NewWithoutIsni (Name.New "John Doe")
            
            authorService.AddAuthorAsync author |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None (Year.New 1924) (Isbn.NewEmpty())
            bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let removeResult = 
                (authorService :> IAuthorService).RemoveAsync author.AuthorId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isError removeResult "should be error as author has books"
    ]

    |> testSequenced
