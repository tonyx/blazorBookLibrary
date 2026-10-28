namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.RateLimiting
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System.Threading.Tasks

[<ApiController>]
[<Route("api/[controller]")>]
[<EnableRateLimiting("AiServicesPolicy")>]
type LibrarianChatController(librarianChatService: ILibrarianChatService) =
    inherit ControllerBase()

    [<HttpPost("chat")>]
    member this.Chat([<FromBody>] request: LibrarianChatRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = librarianChatService.ChatAsync(context, request)
            match result with
            | Ok response -> return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
