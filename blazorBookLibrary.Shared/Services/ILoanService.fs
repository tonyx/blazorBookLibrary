
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type ILoanService =
    abstract member AddLoanAsync: loan: Loan * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member GetLoanAsync: id: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<Loan,string>>
    abstract member ReleaseLoanAsync: loanId: LoanId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit,string>>