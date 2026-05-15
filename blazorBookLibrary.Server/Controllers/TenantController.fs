
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open BookLibrary.Services

[<ApiController>]
[<Route("api/[controller]")>]
type TenantController(tenantService: ITenantService) =
    inherit ControllerBase()

    [<HttpPost>]
    member this.CreateTenant([<FromBody>] tenant: Tenant) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.CreateTenantAsync(context, tenant)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}")>]
    member this.GetTenant(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GetTenantAsync(context, TenantId id)
            match result with
            | Ok tenant -> return this.Ok(tenant) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
