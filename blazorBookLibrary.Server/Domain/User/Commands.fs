
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type UserCommand =
    | AddReservation of ReservationId
    | RemoveReservation of ReservationId
    | AddLoan of LoanId
    | ReleaseLoan of LoanId
    | LoanFromReservation of LoanId * ReservationId
    | GdprGhost
    interface AggregateCommand<User, UserEvent> with
        member this.Execute (user: User) =
            match this with
            | AddReservation reservationId ->
                user.AddReservation reservationId
                |> Result.map (fun u -> (u, [ReservationAdded(reservationId)]))
            | RemoveReservation reservationId ->
                user.RemoveReservation reservationId
                |> Result.map (fun u -> (u, [ReservationRemoved(reservationId)]))
            | AddLoan loanId ->
                user.AddLoan loanId
                |> Result.map (fun u -> (u, [LoanAdded(loanId)]))
            | ReleaseLoan loanId ->
                user.ReleaseLoan loanId
                |> Result.map (fun u -> (u, [LoanReleased(loanId)]))
            | LoanFromReservation (loanId, reservationId) ->
                user.ConvertReservationToLoan loanId reservationId
                |> Result.map (fun u -> (u, [LoanTakenFromReservation(loanId, reservationId)]))
            | GdprGhost ->
                user.GdprGhost()
                |> Result.map (fun u -> (u, [GdprGhosted]))

        member this.Undoer = None
