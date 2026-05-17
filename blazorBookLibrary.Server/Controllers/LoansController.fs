
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
type LoansController(loanService: ILoanService, userService: IUserService) =
    inherit ControllerBase()

    [<HttpGet>]
    member this.GetLoans() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = loanService.GetLoansAsync(context)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("history/{userId}")>]
    member this.GetHistory(userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = loanService.GetHistoryLoansOfUserAsync(context, UserId userId)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddLoan(loan: Loan, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let shortLang = if System.String.IsNullOrWhiteSpace(lang) then (ShortLang.New "it") else (ShortLang.New lang)
            let! result = loanService.AddLoanAsync(context, loan, shortLang)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("{id}")>]
    member this.GetLoan(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = loanService.GetLoanAsync(context, LoanId id)
            match result with
            | Ok loan -> return this.Ok(loan) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpPost("release/{id}")>]
    member this.ReleaseLoan(id: Guid, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let shortLang = if System.String.IsNullOrWhiteSpace(lang) then (ShortLang.New "it") else (ShortLang.New lang)
            let! result = loanService.ReleaseLoanAsync(context, LoanId id, shortLang, DateTime.UtcNow)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("transform-reservation/{reservationId}")>]
    member this.TransformReservation(reservationId: Guid, [<FromBody>] reservationCode: string, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let shortLang = if System.String.IsNullOrWhiteSpace(lang) then (ShortLang.New "it") else (ShortLang.New lang)
            let! result = loanService.TransformReservationIntoLoanAsync(context, ReservationId reservationId, ReservationCode reservationCode, shortLang, DateTime.UtcNow)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
