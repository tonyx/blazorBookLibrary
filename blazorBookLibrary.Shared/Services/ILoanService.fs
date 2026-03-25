
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Definitions
open Sharpino.Core
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details

type ILoanService =
    abstract member AddLoanAsync: loan: Loan * ?ct: CancellationToken -> Task<Result<unit,string>>
    abstract member GetLoanAsync: id: LoanId * ?ct: CancellationToken -> Task<Result<Loan,string>>
    abstract member ReleaseLoanAsync: loanId: LoanId * ?ct: CancellationToken -> Task<Result<unit,string>>