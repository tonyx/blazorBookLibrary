
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
type UsersController(userService: IUserService) =
    inherit ControllerBase()

    [<HttpGet("{id}")>]
    member this.GetUser(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = userService.GetUserAsync(context, UserId id)
            match result with
            | Ok user -> return this.Ok(user) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("{id}/details")>]
    member this.GetUserDetails(id: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = userService.GetUserDetailsAsync(context, UserId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpPost("{id}/fiscal-code")>]
    member this.SetFiscalCode(id: Guid, [<FromBody>] fiscalCode: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            match FiscalCode.New fiscalCode with
            | Ok fc -> 
                let! result = userService.SetFiscalCodeAsync(context, UserId id, fc)
                match result with
                | Ok _ -> return this.Ok() :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/name")>]
    member this.SetName(id: Guid, [<FromBody>] name: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = userService.SetNameAsync(context, UserId id, name)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/surname")>]
    member this.SetSurname(id: Guid, [<FromBody>] surname: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let! result = userService.SetSurnameAsync(context, UserId id, surname)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/phone")>]
    member this.SetPhoneNumber(id: Guid, [<FromBody>] phoneNumber: string) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            match PhoneNumber.New phoneNumber with
            | Ok pn -> 
                let! result = userService.SetPhoneNumberAsync(context, UserId id, pn)
                match result with
                | Ok _ -> return this.Ok() :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}/managed-distribution-points")>]
    member this.GetManagedDistributionPoints(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = userService.GetDistributionPointsManagedByUserAsync(context, UserId id)
            match result with
            | Ok dps -> return this.Ok(dps) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
