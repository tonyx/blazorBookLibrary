
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Domain
open System.Threading.Tasks
open System
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
[<Produces("application/json")>]
type DetailsController(detailsService: IDetailsService, userService: IUserService) =
    inherit ControllerBase()

    [<HttpGet("book/{id}")>]
    member this.GetBookDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetBookDetailsAsync(context, BookId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("loan/{id}")>]
    member this.GetLoanDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetLoanDetailsAsync(context, LoanId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("loans")>]
    member this.GetAllLoansDetails() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetAllLoansDetailsAsync(context)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("reservation/{id}")>]
    member this.GetReservationDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetReservationDetailsAsync(context, ReservationId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("reservations/pending")>]
    member this.GetAllPendingReservationsDetails() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetAllPendingReservationsDetailsAsync(context)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("user/{id}")>]
    member this.GetUserDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetUserDetailsAsync(context, UserId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("author/{id}")>]
    member this.GetAuthorDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetAuthorDetailsAsync(context, AuthorId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("review/{id}")>]
    member this.GetReviewDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetReviewDetailsAsync(context, ReviewId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet("reviews")>]
    member this.GetAllReviewsDetails() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetAllReviewsDetailsAsync(context)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("reviews/book/{bookId}")>]
    member this.GetApprovedVisibleReviewsOfBook([<FromRoute>] bookId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetApprovedVisibleReviewsOfBookAsync(context, BookId bookId)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("tenant/{id}")>]
    member this.GetTenantDetails([<FromRoute>] id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! context = UserContextMapper.enrichContextAsync userService context
            let! result = detailsService.GetTenantDetailsAsync(context, TenantId id)
            match result with
            | Ok details -> return this.Ok(details) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }
