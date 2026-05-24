namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type ReservationCommand =
    | CancelByUser of Cancellation * DateTime * UserId
    | CancelByLibrarian of Cancellation * DateTime
    | Seal of DateTime
    | Unseal of DateTime
    | Loan of DateTime
    | GeneratePickupPin of string * DateTime
    | VerifyPickupPin of string * DateTime
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
            | Loan dateTime ->
                reservation.Loan dateTime
                |> Result.map (fun r -> (r, [ReservationLoaned(dateTime)]))
            | GeneratePickupPin (pinHash, expiresAt) ->
                reservation.GeneratePickupPin (pinHash, expiresAt)
                |> Result.map (fun r -> (r, [PickupPinGenerated2(pinHash, expiresAt)]))
            | VerifyPickupPin (pinHash, dateTime) ->
                match reservation.PickupPinHash, reservation.PickupPinExpiresAt with
                | Some storedHash, Some expiresAt ->
                    if storedHash = pinHash && expiresAt >= dateTime then
                        reservation.VerifyPickupPinAndLoan dateTime
                        |> Result.map (fun r -> (r, [PickupPinVerified2(dateTime)]))
                    else if expiresAt < dateTime then
                        Error "Pickup PIN has expired"
                    else
                        Error "Invalid Pickup PIN"
                | _ -> Error "No active Pickup PIN for this reservation"

        member this.Undoer = None
