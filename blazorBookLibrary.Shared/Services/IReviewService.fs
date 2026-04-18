namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open blazorBookLibrary.Data

type IReviewService = 
    abstract member GetReviewAsync : commentId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<Review, string>>
    abstract member GetAllReviewsAsync : [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<Review>, string>>
    abstract member GetPendingReviewsAsync : [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<Review>, string>>
    abstract member AddReviewAsync : review:Review * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member EditReviewAsync : reviewId:ReviewId * editedComment:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ApproveAsync: reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RejectAsync: reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ShowAsync: reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member HideAsync: reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetReviewsOfBookAsync: bookId:BookId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<ApplicationUser * Review>, string>>
    abstract member GetReviewsOfUserAsync: userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<Book * Review>, string>>
    

    

    
        