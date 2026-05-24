
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type LoansController(loanService: ILoanService) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetLoans() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetLoansAsync(context)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("unarchived")>]
    member this.GetUnarchivedLoans() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetUnarchivedLoansAsync(context)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("history/{userId}")>]
    member this.GetHistory(userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetHistoryLoansOfUserAsync(context, UserId userId)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddLoan(loan: Loan) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.AddLoanAsync(context, loan)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}")>]
    member this.GetLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetLoanAsync(context, LoanId id)
            match result with
            | Ok loan -> return this.Ok(loan) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpPost("release/{id}")>]
    member this.ReleaseLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.ReleaseLoanAsync(context, LoanId id, DateTime.UtcNow)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("transform-reservation/{reservationId}")>]
    member this.TransformReservation(reservationId: Guid, [<FromBody>] reservationCode: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.TransformReservationIntoLoanAsync(context, ReservationId reservationId, ReservationCode reservationCode, DateTime.UtcNow)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("transform-reservation-by-pin/{reservationId}")>]
    member this.TransformReservationByPin(reservationId: Guid, [<FromBody>] pin: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.TransformReservationIntoLoanByPinAsync(context, ReservationId reservationId, pin, DateTime.UtcNow)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }


    [<HttpDelete("{id}")>]
    member this.RemoveLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.RemoveLoanAsync(context, LoanId id)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("archive/{id}")>]
    member this.ArchiveLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.ArchiveLoanAsync(context, LoanId id)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
    [<HttpGet("tenant/{tenantId}/user/{userId}")>]
    member this.GetLoansOfUserInTenant(tenantId: Guid, userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetLoansOfUserInATenantAsync(context, TenantId tenantId, UserId userId)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
