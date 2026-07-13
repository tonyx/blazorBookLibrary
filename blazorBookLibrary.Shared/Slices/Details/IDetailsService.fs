namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type IDetailsService =
    abstract member GetBookDetailsAsync: context:UserContext * id: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<BookDetails,string>>
    abstract member GetLoanDetailsAsync: context:UserContext * id: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<LoanDetails,string>>
    abstract member GetAllLoansDetailsAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<LoanDetails>,string>>
    abstract member GetReservationDetailsAsync: context:UserContext * id: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<ReservationDetails,string>>
    abstract member GetAllPendingReservationsDetailsAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<ReservationDetails>,string>>
    abstract member GetUserDetailsAsync: context:UserContext * id: UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<UserDetails,string>>
    abstract member GetAuthorDetailsAsync: context:UserContext * id: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<AuthorDetails,string>>
    abstract member GetAuthorsDetailsAsync: context:UserContext * ids: List<AuthorId> * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<AuthorDetails>,string>>
    abstract member GetReviewDetailsAsync: context:UserContext * id: ReviewId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<ReviewDetails,string>>
    abstract member GetAllReviewsDetailsAsync: context:UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<ReviewDetails>,string>>
    abstract member GetApprovedVisibleReviewsOfBookAsync: context:UserContext * bookId:BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<ReviewDetails>,string>>
    abstract member GetTenantDetailsAsync: context:UserContext * id: TenantId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<TenantDetails,string>>

    
