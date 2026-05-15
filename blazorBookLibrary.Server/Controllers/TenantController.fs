
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open BookLibrary.Services
open BookLibrary.Shared

type PatronRegistration = { UserId: Guid; Role: PatronRole }

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

    [<HttpPost("{id}/patrons")>]
    member this.AddPatron(id: Guid, [<FromBody>] patron: PatronRegistration) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.AddPatronAsync(context, TenantId id, UserId patron.UserId, patron.Role)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/patrons/{userId}/demote")>]
    member this.DemotePatron(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.DemotePatronAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/patrons/{userId}/promote")>]
    member this.PromotePatron(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.PromotePatronAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/patrons/{userId}")>]
    member this.RemovePatron(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.RemovePatronAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}/patrons/{userId}/role")>]
    member this.GetUserRole(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GetUserRoleAsync(context, TenantId id, UserId userId)
            match result with
            | Ok role -> return this.Ok(role) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("public")>]
    member this.GetAllPublicTenants() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GetAllPublicTenantsAsync(context)
            match result with
            | Ok tenants -> return this.Ok(tenants) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("allowed")>]
    member this.GetAllowedTenants() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GetAllowedTenantsAsync(context)
            match result with
            | Ok tenants -> return this.Ok(tenants) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("my")>]
    member this.GetMyTenants() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GetMyTenantsAsync(context)
            match result with
            | Ok tenants -> return this.Ok(tenants) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("owned")>]
    member this.GetMyOwnedTenants() =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GetMyOwnedTenantsAsync(context)
            match result with
            | Ok tenants -> return this.Ok(tenants) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
