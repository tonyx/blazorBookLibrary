namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type IReviewService = 
    abstract member GetReviewAsync : context:UserContext * commentId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<Review, string>>
    abstract member GetAllReviewsAsync : context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<Review>, string>>
    abstract member GetPendingReviewsAsync : context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<Review>, string>>
    abstract member AddReviewAsync : context:UserContext * review:Review * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member EditReviewAsync : context:UserContext * reviewId:ReviewId * editedComment:string * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ApproveAsync: context:UserContext * reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RejectAsync: context:UserContext * reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ShowAsync: context:UserContext * reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member HideAsync: context:UserContext * reviewId:ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetReviewsOfBookAsync: context:UserContext * bookId:BookId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<AppUserInfo * Review>, string>>
    abstract member GetApprovedVisibleReviewsOfBookAsync: context:UserContext * bookId:BookId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<AppUserInfo * Review>, string>>
    abstract member GetReviewsOfUserAsync: context:UserContext * userId:UserId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<list<Book * Review>, string>>
    
    