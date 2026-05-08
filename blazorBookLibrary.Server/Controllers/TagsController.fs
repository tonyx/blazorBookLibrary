
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
type TagsController(tagService: ITagService) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetTags() =
        task {
            let! result = tagService.GetTagsAsync()
            match result with
            | Ok tags -> return this.Ok(tags) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddTag(tag: Tag) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = tagService.AddTagAsync(context, tag)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete>]
    member this.RemoveTag(tag: Tag) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = tagService.RemoveTagAsync(context, tag)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("books")>]
    member this.GetBookTags() =
        task {
            let! result = tagService.GetBookTypeTagsAsync()
            match result with
            | Ok tags -> return this.Ok(tags) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("authors")>]
    member this.GetAuthorTags() =
        task {
            let! result = tagService.GetAuthorTypeTagsAsync()
            match result with
            | Ok tags -> return this.Ok(tags) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
