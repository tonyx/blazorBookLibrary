
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

    [<HttpPost("{id}/patrons/{userId}/invite")>]
    member this.InvitePatron(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.InvitePatronAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/patrons/{userId}/invite")>]
    member this.RevokePatronInvitation(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.RevokePatronInvitation(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/patrons/{userId}/suspend")>]
    member this.SuspendPatron(id: Guid, userId: Guid, [<FromQuery>] reason: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.SuspendPatron(context, TenantId id, UserId userId, reason)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/patrons/{userId}/readmit")>]
    member this.ReAdmittPatron(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.ReAdmittPatron(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/patrons/convert")>]
    member this.ConvertInvitedPatronToPatron(id: Guid, [<FromQuery>] invitationCode: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            match System.Guid.TryParse(invitationCode) with
            | false, _ -> return this.BadRequest("Invalid invitation code format") :> IActionResult
            | true, parsedGuid ->
                let code = PatronInvitationCode.PatronInvitationCode parsedGuid
                let! result = tenantService.ConvertInvitedPatronToPatronAsync(context, TenantId id, code)
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

    [<HttpPut("{id}/public")>]
    member this.SetPublic(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.SetPublicAsync(context, TenantId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/private")>]
    member this.SetPrivate(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.SetPrivateAsync(context, TenantId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}")>]
    member this.DeleteTenant(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = tenantService.DeleteTenantAsync(context, TenantId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("by-pin/{pin}")>]
    member this.FindTenantByJoinPin(pin: string) =
        task {
            let! result = tenantService.FindTenantByJoinPinAsync(pin)
            match result with
            | Ok tenant -> return this.Ok(tenant) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/join-pin")>]
    member this.GenerateJoinPin(id: Guid, [<FromQuery>] pin: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.GenerateJoinPinAsync(context, TenantId id, pin)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/join-requests/{userId}")>]
    member this.SubmitJoinRequest(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.SubmitJoinRequestAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/join-requests/{userId}/approve")>]
    member this.ApproveJoinRequest(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.ApproveJoinRequestAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPut("{id}/join-requests/{userId}/reject")>]
    member this.RejectJoinRequest(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantService.RejectJoinRequestAsync(context, TenantId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
