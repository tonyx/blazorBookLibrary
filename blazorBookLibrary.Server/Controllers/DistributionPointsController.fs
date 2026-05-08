
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

    [<HttpGet("{id}")>]
    member this.GetDistributionPoint(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = distributionPointService.GetDistributionPointAsync(context, DistributionPointId id)
            match result with
            | Ok dp -> return this.Ok(dp) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
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
