
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System
open System.Threading.Tasks

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

    [<HttpGet("search/title")>]
    member this.SearchByTitle(title: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.SearchByTitleAsync(context, Title.New title)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/isbn")>]
    member this.SearchByIsbn(isbn: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = bookService.SearchByIsbnAsync(context, Isbn isbn)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
