
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System
open System.Threading.Tasks

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

    [<HttpGet("history/{userId}")>]
    member this.GetHistory(userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = loanService.GetHistoryLoansOfUserAsync(context, UserId userId)
            match result with
            | Ok loans -> return this.Ok(loans) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("release/{id}")>]
    member this.ReleaseLoan(id: Guid, [<FromQuery>] lang: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let shortLang = if System.String.IsNullOrWhiteSpace(lang) then (ShortLang.New "it") else (ShortLang.New lang)
            let! result = loanService.ReleaseLoanAsync(context, LoanId id, shortLang, DateTime.UtcNow)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
