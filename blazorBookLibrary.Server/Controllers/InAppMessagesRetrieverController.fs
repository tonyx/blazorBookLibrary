namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System
open System.Threading.Tasks

[<ApiController>]
[<Route("api/[controller]")>]
type InAppMessagesRetrieverController(inAppMessagesRetriever: IInAppMessagesRetriever) =
    inherit ControllerBase()

    [<HttpPost("loan")>]
    member this.GetLoanNotificationInApp([<FromBody>] request: GetLoanNotificationRequest) =
        task {
            let bookTitle = Title.New request.BookTitle
            let tenantNameResult = TenantName.New request.TenantName
            let shortLang = ShortLang.New request.ShortLang

            match tenantNameResult with
            | Ok tenantName ->
                let! result = inAppMessagesRetriever.GetLoanNotificationInAppAsync(bookTitle, tenantName, request.DistributionPoint, request.LoanDate, request.DueDate, shortLang)
                match result with
                | Ok content -> return this.Ok(content) :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | Error err ->
                return this.BadRequest(err) :> IActionResult
        }

    [<HttpPost("release-loan")>]
    member this.GetReleaseLoanNotificationInApp([<FromBody>] request: GetReleaseLoanNotificationRequest) =
        task {
            let bookTitle = Title.New request.BookTitle
            let tenantNameResult = TenantName.New request.TenantName
            let dpNameResult = NonEmptyName.New request.DpName
            let shortLang = ShortLang.New request.ShortLang

            match tenantNameResult, dpNameResult with
            | Ok tenantName, Ok dpName ->
                let! result = inAppMessagesRetriever.GetReleaseLoanNotificationInAppAsync(request.UserName, bookTitle, request.LoanedAt, request.ReturnedAt, tenantName, dpName, shortLang)
                match result with
                | Ok content -> return this.Ok(content) :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | Error err, _ ->
                return this.BadRequest(err) :> IActionResult
            | _, Error err ->
                return this.BadRequest(err) :> IActionResult
        }

    [<HttpPost("reservation")>]
    member this.GetReservationNotificationInApp([<FromBody>] request: GetReservationNotificationRequest) =
        task {
            let bookTitle = Title.New request.BookTitle
            let code = ReservationCode.ReservationCode request.Code
            let tenantNameResult = TenantName.New request.TenantName
            let shortLang = ShortLang.New request.ShortLang

            match tenantNameResult with
            | Ok tenantName ->
                let! result = inAppMessagesRetriever.GetReservationNotificationInAppAsync(bookTitle, code, tenantName, request.DistributionPoint, shortLang)
                match result with
                | Ok content -> return this.Ok(content) :> IActionResult
                | Error msg -> return this.BadRequest(msg) :> IActionResult
            | Error err ->
                return this.BadRequest(err) :> IActionResult
        }

    [<HttpGet("patron-invitation")>]
    member this.GetPatronInvitationInApp([<FromQuery>] shortLang: string) =
        task {
            let shortLangObj = ShortLang.New shortLang
            let! result = inAppMessagesRetriever.GetPatronInvitationInAppAsync(shortLangObj)
            match result with
            | Ok content -> return this.Ok(content) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
