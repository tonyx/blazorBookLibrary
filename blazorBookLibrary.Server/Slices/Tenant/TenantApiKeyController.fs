namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open BookLibrary.Services
open BookLibrary.Shared

[<CLIMutable>]
type GeneratePartnerKeyRequest =
    {
        PartnerId: Guid
        Label: string
        Now: Nullable<DateTime>
        ExpiresAt: Nullable<DateTime>
    }

[<ApiController>]
[<Route("api/TenantApiKey")>]
type TenantApiKeyController(tenantApiKeyService: ITenantApiKeyService) =
    inherit ControllerBase()

    [<HttpGet("{tenantId}")>]
    member this.GetPartnerKeys(tenantId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantApiKeyService.GetPartnerKeysAsync(context, TenantId tenantId)
            match result with
            | Ok keys -> return this.Ok(keys) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{tenantId}")>]
    member this.AddPartnerKey(tenantId: Guid, [<FromBody>] partnerApiKey: PartnerApiKey) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantApiKeyService.AddPartnerKeyAsync(context, TenantId tenantId, partnerApiKey)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{tenantId}/generate")>]
    member this.GeneratePartnerKey(tenantId: Guid, [<FromBody>] req: GeneratePartnerKeyRequest) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let now = if req.Now.HasValue then req.Now.Value else DateTime.UtcNow
            let expiresAtOpt = if req.ExpiresAt.HasValue then Some req.ExpiresAt.Value else None
            let! result = tenantApiKeyService.AddPartnerKeyAsync(context, TenantId tenantId, UserId req.PartnerId, req.Label, now, ?expiresAt = expiresAtOpt)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{tenantId}/{key}/revoke")>]
    [<HttpDelete("{tenantId}/{key}/revoke")>]
    member this.RevokeCatalogKey(tenantId: Guid, key: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantApiKeyService.RevokeCatalogKeyAsync(context, TenantId tenantId, CatalogApiKey key)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{tenantId}/{key}")>]
    member this.RemovePartnerKey(tenantId: Guid, key: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = tenantApiKeyService.RemovePartnerKeyAsync(context, TenantId tenantId, CatalogApiKey key)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{tenantId}/validate/{key}")>]
    member this.ValidateCatalogKey(tenantId: Guid, key: string) =
        task {
            let! result = tenantApiKeyService.ValidateCatalogKeyAsync(TenantId tenantId, CatalogApiKey key)
            match result with
            | Ok () -> return this.Ok({| valid = true |}) :> IActionResult
            | Error msg -> return this.Unauthorized({| message = msg; valid = false |}) :> IActionResult
        }
