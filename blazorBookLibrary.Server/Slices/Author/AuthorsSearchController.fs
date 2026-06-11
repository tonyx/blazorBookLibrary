
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System.Threading.Tasks

[<ApiController>]
[<Route("api/[controller]")>]
type AuthorsSearchController(authorsSearchService: IAuthorsSearchService) =
    inherit ControllerBase()

    [<HttpGet("lookup")>]
    member this.LookupByName(name: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = authorsSearchService.LookupByNameAsync(context, name)
            match result with
            | Ok author -> return this.Ok(author) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("image")>]
    member this.LookupImageUrl(name: string, [<FromQuery>] thumbSize: System.Nullable<int>) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let thumbSizeOpt = if thumbSize.HasValue then Some thumbSize.Value else None
            let! result = authorsSearchService.LookupImageUrlByNameAndThumbSizeAsync(context, name, ?pitThumbSize = thumbSizeOpt)
            match result with
            | Ok url -> return this.Ok(url) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("bio")>]
    member this.LookupBio(name: string, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let shortLang = if System.String.IsNullOrWhiteSpace(lang) then (ShortLang.New "it") else (ShortLang.New lang)
            let! result = authorsSearchService.LookupBioByNameAsync(context, name, lang = shortLang)
            match result with
            | Ok bios -> return this.Ok(bios) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("wikipedia")>]
    member this.LookupWikipediaUri(name: string, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let shortLang = if System.String.IsNullOrWhiteSpace(lang) then (ShortLang.New "it") else (ShortLang.New lang)
            let! result = authorsSearchService.LookupWikipediaUriByNameAsync(context, name, lang = shortLang)
            match result with
            | Ok uri -> return this.Ok(uri) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
