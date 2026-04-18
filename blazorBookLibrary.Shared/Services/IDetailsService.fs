namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IDetailsService =
    abstract member GetBookDetailsAsync: id: BookId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<BookDetails,string>>
    abstract member GetLoanDetailsAsync: id: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<LoanDetails,string>>
    abstract member GetAllLoansDetailsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<LoanDetails>,string>>
    abstract member GetReservationDetailsAsync: id: ReservationId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<ReservationDetails,string>>
    abstract member GetAllPendingReservationsDetailsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<ReservationDetails>,string>>
    abstract member GetUserDetailsAsync: id: UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<UserDetails,string>>
    abstract member GetAuthorDetailsAsync: id: AuthorId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<AuthorDetails,string>>
    
