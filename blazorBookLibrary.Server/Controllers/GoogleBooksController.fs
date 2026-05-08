
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
type GoogleBooksController(googleBooksService: IGoogleBooksService) =
    inherit ControllerBase()

    [<HttpGet("lookup/isbn/{isbn}")>]
    member this.LookupByIsbn(isbn: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = googleBooksService.LookupByIsbnAsync(context, isbn)
            match result with
            | Ok metadata -> return this.Ok(metadata) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("lookup/title/{title}")>]
    member this.LookupByTitle(title: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = googleBooksService.LookupByTitleAsync(context, title)
            match result with
            | Ok metadata -> return this.Ok(metadata) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("lookup/multiple/title/{title}")>]
    member this.LookupMultipleByTitle(title: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = googleBooksService.LookupMultipleByTitleAsync(context, title)
            match result with
            | Ok results -> return this.Ok(results) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("cover/isbn/{isbn}")>]
    member this.LookupCoverImage(isbn: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = googleBooksService.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(context, Isbn.New isbn |> (fun r -> match r with | Ok i -> i | Error _ -> EmptyIsbn))
            match result with
            | Ok url -> return this.Ok(url) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
