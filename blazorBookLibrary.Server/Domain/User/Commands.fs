
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type UserCommand =
    | AddFutureReservation of ReservationId
    | RemoveFutureReservation of ReservationId
    | AddLoan of LoanId
    | ReleaseLoan of LoanId
    | LoanFromReservation of LoanId * ReservationId
    interface AggregateCommand<User, UserEvent> with
        member this.Execute (user: User) =
            match this with
            | AddFutureReservation reservationId ->
                user.AddFutureReservation reservationId
                |> Result.map (fun u -> (u, [FutureReservationAdded(reservationId)]))
            | RemoveFutureReservation reservationId ->
                user.RemoveFutureReservation reservationId
                |> Result.map (fun u -> (u, [FutureReservationRemoved(reservationId)]))
            | AddLoan loanId ->
                user.AddLoan loanId
                |> Result.map (fun u -> (u, [LoanAdded(loanId)]))
            | ReleaseLoan loanId ->
                user.ReleaseLoan loanId
                |> Result.map (fun u -> (u, [LoanReleased(loanId)]))
            | LoanFromReservation (loanId, reservationId) ->
                user.LoanFromReservation loanId reservationId
                |> Result.map (fun u -> (u, [LoanTakenFromReservation(loanId, reservationId)]))

        member this.Undoer = None
