
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
type AdminController(adminService: IAdminServices) =
    inherit ControllerBase()

    [<HttpPost("vectors/purge")>]
    member this.PurgeVectors() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.PurgeVectorsReferringDroppedBooksAsync(context)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("vectors/purge-duplicates")>]
    member this.PurgeDuplicatedVectors() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.PurgeDuplicatedVectorsAsync(context)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("books/adjust-states")>]
    member this.AdjustBookStates() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.AdjustBookStatesReferringMissingEmbeddingsAsync(context)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("distribution-points/{id}/assign-user/{userId}")>]
    member this.AssignUserToDistributionPoint(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.AssignUserToDistributionPointAsync(context, DistributionPointId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("distribution-points/{id}/unassign-user/{userId}")>]
    member this.UnassignUserFromDistributionPoint(id: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.UnassignUserFromDistributionPointAsync(context, DistributionPointId id, UserId userId)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("distribution-points/{id}/rename")>]
    member this.RenameDistributionPoint(id: Guid, [<FromBody>] newName: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            match NonEmptyName.New newName with
            | Ok name -> 
                let! result = adminService.RenameDistributionPointAsync(context, DistributionPointId id, name)
                match result with
                | Ok _ -> return this.Ok() :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("recover/aggregate/{aggregateType}/{aggregateId}")>]
    member this.RecoverAggregateStream(aggregateType: string, aggregateId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.RecoverAggregateStreamAsync(context, aggregateType, aggregateId)
            match result with
            | Ok msg -> return this.Ok(msg) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("recover/all/{aggregateType}")>]
    member this.RecoverAllAggregates(aggregateType: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = adminService.RecoverAllAggregatesAsync(context, aggregateType)
            match result with
            | Ok msg -> return this.Ok(msg) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
