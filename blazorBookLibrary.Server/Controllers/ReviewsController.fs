
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
type ReviewsController(reviewService: IReviewService) =
    inherit ControllerBase()

    [<HttpGet("{id}")>]
    member this.GetReview(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.GetReviewAsync(context, ReviewId id)
            match result with
            | Ok review -> return this.Ok(review) :> IActionResult
            | Error msg -> return this.NotFound(msg) :> IActionResult
        }

    [<HttpGet>]
    member this.GetAllReviews() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.GetAllReviewsAsync(context)
            match result with
            | Ok reviews -> return this.Ok(reviews) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("pending")>]
    member this.GetPendingReviews() =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.GetPendingReviewsAsync(context)
            match result with
            | Ok reviews -> return this.Ok(reviews) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost>]
    member this.AddReview(review: Review) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.AddReviewAsync(context, review)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/approve")>]
    member this.ApproveReview(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.ApproveAsync(context, ReviewId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("{id}/reject")>]
    member this.RejectReview(id: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.RejectAsync(context, ReviewId id)
            match result with
            | Ok _ -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("book/{bookId}")>]
    member this.GetReviewsOfBook(bookId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.GetReviewsOfBookAsync(context, BookId bookId)
            match result with
            | Ok reviews -> return this.Ok(reviews) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("user/{userId}")>]
    member this.GetReviewsOfUser(userId: Guid) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let! result = reviewService.GetReviewsOfUserAsync(context, UserId userId)
            match result with
            | Ok reviews -> return this.Ok(reviews) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
