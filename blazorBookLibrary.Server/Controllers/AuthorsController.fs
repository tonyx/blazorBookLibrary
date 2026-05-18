
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

    [<HttpPost("bulk")>]
    member this.AddAuthors(authors: List<Author>) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.AddAuthorsAsync(context, authors |> List.ofSeq)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("get-multiple")>]
    member this.GetAuthors([<FromBody>] ids: List<Guid>) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let authorIds = ids |> Seq.map AuthorId |> List.ofSeq
            let! result = authorService.GetAuthorsAsync(context, authorIds)
            match result with
            | Ok authors -> return this.Ok(authors) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/image-url")>]
    member this.UpdateImageUrl(id: Guid, [<FromBody>] imageUrl: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            match Uri.TryCreate(imageUrl, UriKind.Absolute) with
            | (true, uri) ->
                let! result = authorService.UpdateImageUrlAsync(context, AuthorId id, uri)
                match result with
                | Ok _ -> return this.Ok() :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | _ -> return this.BadRequest("Invalid image URL") :> IActionResult
        }

    [<HttpDelete("{id}/image-url")>]
    member this.RemoveImageUrl(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.RemoveImageUrlAsync(context, AuthorId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/isni")>]
    member this.UpdateIsni(id: Guid, [<FromBody>] isni: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.UpdateIsniAsync(context, AuthorId id, Isni.NewInvalid isni)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/bio")>]
    member this.UpdateBio(id: Guid, [<FromBody>] bio: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.UpdateBioAsync(context, AuthorId id, bio)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/wikipedia-uri")>]
    member this.UpdateWikipediaUri(id: Guid, [<FromBody>] wikipediaUri: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            match Uri.TryCreate(wikipediaUri, UriKind.Absolute) with
            | (true, uri) ->
                let! result = authorService.UpdateWikipediaUriAsync(context, AuthorId id, uri)
                match result with
                | Ok _ -> return this.Ok() :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | _ -> return this.BadRequest("Invalid Wikipedia URI") :> IActionResult
        }

    [<HttpPost("{id}/seal")>]
    member this.Seal(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.SealAsync(context, AuthorId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/unseal")>]
    member this.Unseal(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.UnsealAsync(context, AuthorId id)
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

    [<HttpGet("search/isni/{isni}")>]
    member this.SearchByIsni(isni: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.SearchByIsniAsync(context, Isni.NewInvalid isni)
            match result with
            | Ok authors -> return this.Ok(authors) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("search/isni/{isni}/name/{name}")>]
    member this.SearchByIsniAndName(isni: string, name: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorService.SearchByIsniAndNameAsync(context, Isni.NewInvalid isni, Name.New name)
            match result with
            | Ok authors -> return this.Ok(authors) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
