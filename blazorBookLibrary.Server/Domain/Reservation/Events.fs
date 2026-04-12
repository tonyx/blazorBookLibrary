
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons
open System.Text.Json

type ReservationEvent =
    | CanceledByUser of Cancellation * DateTime * UserId
    | CanceledByLibrarian of Cancellation * DateTime
    | ReservationSealed of DateTime
    | ReservationUnsealed of DateTime
    | ReservationLoaned of DateTime
    interface Event<Reservation> with
        member this.Process (reservation: Reservation) =
            match this with
            | CanceledByUser (cancellation, dateTime, userId) ->
                reservation.CancelByUser cancellation dateTime userId
            | CanceledByLibrarian (cancellation, dateTime) ->
                reservation.CancelByLibrarian cancellation dateTime
            | ReservationSealed dateTime ->
                reservation.Seal dateTime
            | ReservationUnsealed dateTime ->
                reservation.Unseal dateTime
            | ReservationLoaned dateTime ->
                reservation.Loan dateTime

    static member Deserialize (x: string): Result<ReservationEvent, string> =
        try
            JsonSerializer.Deserialize<ReservationEvent> (x, jsonOptions) |> Ok
        with
            | ex -> Error ex.Message
    
    member this.Serialize =
        JsonSerializer.Serialize (this, jsonOptions)
