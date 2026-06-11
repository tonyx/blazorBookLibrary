
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
type DistributionPointsController(distributionPointService: IDistributionPointService) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetAllDistributionPoints() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.GetAllDistributionPointsAsync(context)
            match result with
            | Ok dps -> return this.Ok(dps) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("tenant/{tenantId}")>]
    member this.GetAllDistributionPointsOfATenant(tenantId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.GetAllDistributionPointsOfATenantAsync(context, TenantId tenantId)
            match result with
            | Ok dps -> return this.Ok(dps) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("managed-by-user/{userId}")>]
    member this.GetAllDistributionPointsManagedByUser(userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.GetAllDistributionPointsManagedByUser(context, UserId userId)
            match result with
            | Ok dps -> return this.Ok(dps) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }


    [<HttpGet("{id}")>]
    member this.GetDistributionPoint(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.GetDistributionPointAsync(context, DistributionPointId id)
            match result with
            | Ok dp -> return this.Ok(dp) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("{id}/books")>]
    member this.GetAllBooksOfADistributionPoint(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.GetAllBooksOfADistributionPointAsync(context, DistributionPointId id)
            match result with
            | Ok books -> return this.Ok(books) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}/is-removable")>]
    member this.IsRemovable(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.IsRemovableAsync(context, DistributionPointId id)
            match result with
            | Ok isRemovable -> return this.Ok(isRemovable) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("find/{name}")>]
    member this.FindDistributionPoints(name: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.FindDistributionPointsAsync(context, Name.New name)
            match result with
            | Ok dps -> return this.Ok(dps) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.CreateDistributionPoint(dp: DistributionPoint) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.CreateDistributionPointAsync(context, dp)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}")>]
    member this.RemoveDistributionPoint(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.RemoveDistributionPointAsync(context, DistributionPointId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/reference-user/{userId}")>]
    member this.AddReferenceUser(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.AddReferenceUser(context, DistributionPointId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}/reference-user/{userId}")>]
    member this.RemoveReferenceUser(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.RemoveReferenceUser(context, DistributionPointId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

