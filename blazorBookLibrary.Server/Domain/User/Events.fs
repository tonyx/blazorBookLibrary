
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type UserEvent =
    | FutureReservationAdded of ReservationId
    | FutureReservationRemoved of ReservationId
    | LoanAdded of LoanId
    | LoanReleased of LoanId
    | LoanTakenFromReservation of LoanId * ReservationId
    interface Event<User> with
        member this.Process (user: User) : Result<User, string> =
            match this with
            | FutureReservationAdded reservationId ->
                user.AddFutureReservation reservationId
            | FutureReservationRemoved reservationId ->
                user.RemoveFutureReservation reservationId
            | LoanAdded loanId ->
                user.AddLoan loanId
            | LoanReleased loanId ->
                user.ReleaseLoan loanId
            | LoanTakenFromReservation (loanId, reservationId) ->
                user.LoanFromReservation loanId reservationId

    static member Deserialize (x: string): Result<UserEvent, string> =
        try
            JsonSerializer.Deserialize<UserEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
