namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open BookLibrary.Services
open BookLibrary.Shared

[<ApiController>]
[<Route("api/[controller]")>]
type UserTenantResolverController(resolverService: IUserTenantResolverService) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetTenantForUser() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = resolverService.GetTenantForUserAsync(context)
            match result with
            | Ok tenantId -> return this.Ok(tenantId) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

