
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type BooksController(bookService: IBookService) =
    inherit ControllerBase()

    [<HttpGet("{id}")>]
    member this.GetBook(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.GetBookAsync(context, BookId id)
            match result with
            | Ok book -> return this.Ok(book) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddBook(book: Book) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.AddBookAsync(context, book)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("bulk")>]
    member this.AddBooks(books: List<Book>) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let fsBooks = List.ofSeq books
            let! result = bookService.AddBooksAsync(context, fsBooks)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet>]
    member this.GetAllBooks() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.GetAllAsync(context)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("get-multiple")>]
    member this.GetBooks([<FromBody>] ids: List<Guid>) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let fsBookIds = List.ofSeq (ids |> Seq.map BookId)
            let! result = bookService.GetBooksAsync(context, fsBookIds)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/title/{title}")>]
    member this.SearchByTitle(title: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.SearchByTitleAsync(context, Title.New title)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/isbn/{isbn}")>]
    member this.SearchByIsbn(isbn: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.SearchByIsbnAsync(context, Isbn isbn)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
