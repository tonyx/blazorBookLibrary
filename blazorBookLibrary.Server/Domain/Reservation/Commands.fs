
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Commons

type ReservationCommand =
    | CancelByUser of Cancellation * DateTime * UserId
    | CancelByLibrarian of Cancellation * DateTime
    | Seal of DateTime
    | Unseal of DateTime
    interface AggregateCommand<Reservation, ReservationEvent> with
        member this.Execute (reservation: Reservation) =
            match this with
            | CancelByUser (cancellation, dateTime, userId) ->
                reservation.CancelByUser cancellation dateTime userId
                |> Result.map (fun r -> (r, [CanceledByUser(cancellation, dateTime, userId)]))
            | CancelByLibrarian (cancellation, dateTime) ->
                reservation.CancelByLibrarian cancellation dateTime
                |> Result.map (fun r -> (r, [CanceledByLibrarian(cancellation, dateTime)]))
            | Seal dateTime ->
                reservation.Seal dateTime
                |> Result.map (fun r -> (r, [ReservationSealed(dateTime)]))
            | Unseal dateTime ->
                reservation.Unseal dateTime
                |> Result.map (fun r -> (r, [ReservationUnsealed(dateTime)]))

        member this.Undoer = None
