
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open System

type ILoanService =
    abstract member AddLoanAsync: context: UserContext * loan: Loan * shortLang: ShortLang * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member GetLoanAsync: context: UserContext * id: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Loan,string>>
    abstract member GetLoansAsync: context: UserContext * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Loan>,string>>
    abstract member ReleaseLoanAsync: context: UserContext * loanId: LoanId * shortLang: ShortLang * date: DateTime * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member TransformReservationIntoLoanAsync: context: UserContext * reservationId: ReservationId * providedReservationCode: ReservationCode * shortLang: ShortLang * date: DateTime * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member GetHistoryLoansOfUserAsync: context: UserContext * userId: UserId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<List<Loan>,string>>