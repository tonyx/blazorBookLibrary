
namespace BookLibrary.Domain
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Commons
open System

type Reservation =
    {
        ReservationId: ReservationId
        BookId: BookId
        UserId: UserId
        TimeSlot: TimeSlot
        ReservedAt: DateTime
        CanceledAt: Option<Cancellation>
        Sealed: Sealed
    } with 
        static member New (bookId: BookId) (userId: UserId) (timeSlot: TimeSlot) (dateTime: DateTime)= 
            {
                ReservationId = ReservationId.New(); 
                BookId = bookId;
                UserId = userId;
                TimeSlot = timeSlot;
                ReservedAt = dateTime;
                CanceledAt = None;
                Sealed = Sealed.New(dateTime)
            }

        member this.CancelByUser (cancellation: Cancellation) (dateTime: DateTime) (userId: UserId) =
            result
                {
                    do! 
                        this.UserId = userId
                        |> Result.ofBool "User is not the one who reserved the book"
                    return { this with CanceledAt = Some cancellation } 
                }

        member this.CancelByLibrarian (cancellation: Cancellation) (dateTime: DateTime) =
            result
                {
                    do!
                        this.Sealed.IsSealed(dateTime)
                        |> not
                        |> Result.ofBool "Reservation is sealed"
                    return { this with CanceledAt = Some cancellation } 
                }

        member this.IsCancelled () =
            this.CanceledAt.IsSome

        member this.Seal(dateTime: DateTime) =
            result
                {
                    do! 
                        this.Sealed.IsSealed(dateTime)
                        |> not
                        |> Result.ofBool "Reservation is sealed"
                    return { this with Sealed = this.Sealed.Seal(dateTime) } 
                }
        member this.Unseal(dateTime: DateTime) =
            { 
                this 
                    with 
                        Sealed = this.Sealed.Unseal(dateTime) 
            } 
            |> Ok
        member this.Id = this.ReservationId.Value
        static member SnapshotsInterval = 50
        static member StorageName = "_Reservation"
        static member Version = "_01"
        member this.Serialize = 
            (this, jsonOptions) |> JsonSerializer.Serialize
        static member Deserialize (data: string) =
            try
                let reservation = JsonSerializer.Deserialize<Reservation> (data, jsonOptions)
                Ok reservation
            with
                | ex -> Error ex.Message