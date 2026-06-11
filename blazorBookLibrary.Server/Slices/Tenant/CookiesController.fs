namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open System
open System.Threading.Tasks

[<ApiController>]
[<Route("api/[controller]")>]
type CookiesController(cookieService: ICookieService) =
    inherit ControllerBase()

    [<HttpGet("{key}")>]
    member this.GetCookie(key: string) =
        task {
            let! valueOpt = cookieService.GetCookieAsync(key)
            match valueOpt with
            | Some v -> return this.Ok(v) :> IActionResult
            | None -> return this.NotFound() :> IActionResult
        }

    [<HttpPost>]
    member this.SetCookie([<FromQuery>] key: string, [<FromQuery>] value: string, [<FromQuery>] ?days: int) =
        task {
            do! cookieService.SetCookieAsync(key, value, ?days = days)
            return this.Ok() :> IActionResult
        }

    [<HttpDelete("{key}")>]
    member this.DeleteCookie(key: string) =
        task {
            do! cookieService.DeleteCookieAsync(key)
            return this.Ok() :> IActionResult
        }
