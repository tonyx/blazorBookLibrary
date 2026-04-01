
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type UserEvent =
    | ReservationAdded of ReservationId
    | ReservationRemoved of ReservationId
    | LoanAdded of LoanId
    | LoanReleased of LoanId
    | LoanTakenFromReservation of LoanId * ReservationId
    interface Event<User> with
        member this.Process (user: User) : Result<User, string> =
            match this with
            | ReservationAdded reservationId ->
                user.AddReservation reservationId
            | ReservationRemoved reservationId ->
                user.RemoveReservation reservationId
            | LoanAdded loanId ->
                user.AddLoan loanId
            | LoanReleased loanId ->
                user.ReleaseLoan loanId
            | LoanTakenFromReservation (loanId, reservationId) ->
                user.ConvertReservationToLoan loanId reservationId

    static member Deserialize (x: string): Result<UserEvent, string> =
        try
            JsonSerializer.Deserialize<UserEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
