
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type LoanEvent =
    | LoanReturned of DateTime
    interface Event<Loan> with
        member this.Process (loan: Loan) =
            match this with
            | LoanReturned dateTime ->
                loan.Return dateTime

    static member Deserialize (x: string): Result<LoanEvent, string> =
        try
            JsonSerializer.Deserialize<LoanEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)