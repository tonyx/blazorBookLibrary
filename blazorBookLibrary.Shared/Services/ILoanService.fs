
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open System

type ILoanService =
    abstract member AddLoanAsync: loan: Loan * shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member GetLoanAsync: id: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Loan,string>>
    abstract member GetLoansAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Loan>,string>>
    abstract member ReleaseLoanAsync: loanId: LoanId * shortLang: ShortLang * date: DateTime * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member TransformReservationIntoLoanAsync: reservationId: ReservationId * providedReservationCode: ReservationCode * shortLang: ShortLang * date: DateTime * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member GetHistoryLoansOfUserAsync: userId: UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Loan>,string>>