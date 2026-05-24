
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
type ReservationsController(reservationService: IReservationService) =
    inherit ControllerBase()

    [<HttpGet("{id}")>]
    member this.GetReservation(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reservationService.GetReservationAsync(context, ReservationId id)
            match result with
            | Ok reservation -> return this.Ok(reservation) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("{id}/details")>]
    member this.GetReservationDetails(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reservationService.GetReservationDetailsAsync(context, ReservationId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddReservation(reservation: Reservation, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let shortLang = if String.IsNullOrWhiteSpace(lang) then ShortLang.New "en" else ShortLang.New lang
            let! result = reservationService.AddReservationAsync(context, reservation, shortLang)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("{id}")>]
    member this.RemoveReservation(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reservationService.RemoveReservationAsync(context, ReservationId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("pending/details")>]
    member this.GetAllPendingReservationsDetails() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reservationService.GetAllPendingReservationsDetailsAsync(context)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/generate-pin")>]
    member this.GeneratePickupPin(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reservationService.GeneratePickupPinAsync(context, ReservationId id)
            match result with
            | Ok (pin, expiresAt) -> 
                let response = dict [("pin", pin :> obj); ("expiresAt", expiresAt :> obj)]
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("book/{bookId}")>]
    member this.GetReservationsOfABook(bookId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reservationService.GetReservationsOfABookAsync(context, BookId bookId)
            match result with
            | Ok reservations -> return this.Ok(reservations) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

