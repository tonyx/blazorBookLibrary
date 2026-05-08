
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System.Threading.Tasks
open System
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type AuthorsController(authorService: IAuthorService) =
    inherit ControllerBase()

    [<HttpPost>]
    member this.AddAuthor(author: Author) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.AddAuthorAsync(context, author)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}")>]
    member this.GetAuthor(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.GetAuthorAsync(context, AuthorId id)
            match result with
            | Ok author -> return this.Ok(author) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("{id}/details")>]
    member this.GetAuthorDetails(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.GetAuthorDetailsAsync(context, AuthorId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet>]
    member this.GetAllAuthors() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.GetAllAsync(context)
            match result with
            | Ok authors -> return this.Ok(authors) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/rename")>]
    member this.RenameAuthor(id: Guid, [<FromBody>] newName: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.RenameAsync(context, AuthorId id, Name.New newName)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}")>]
    member this.DeleteAuthor(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.RemoveAsync(context, AuthorId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/name/{name}")>]
    member this.SearchByName(name: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.SearchByNameAsync(context, Name.New name)
            match result with
            | Ok authors -> return this.Ok(authors) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
