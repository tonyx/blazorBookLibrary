
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type LoanCommand =
    | Return of DateTime
    interface AggregateCommand<Loan, LoanEvent> with
        member this.Execute (loan: Loan) =
            match this with
            | Return dateTime ->
                loan.Return dateTime
                |> Result.map (fun l -> (l, [LoanReturned dateTime]))

        member this.Undoer = None
