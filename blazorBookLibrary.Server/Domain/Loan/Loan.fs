
namespace BookLibrary.Domain
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Commons
open System

type Loan = {
    LoanId: LoanId
    BookId: BookId
    UserId: UserId
    ReservationId: Option<ReservationId>
    LoanedAt: DateTime
    TimeSlot: TimeSlot
    ReturnedAt: DateTime option
} with
    static member New (bookId: BookId) (userId: UserId) (loanedAt: DateTime) (timeSlot: TimeSlot) = 
        {
            LoanId = LoanId.New(); 
            BookId = bookId;
            UserId = userId;
            LoanedAt = loanedAt;
            TimeSlot = timeSlot;
            ReservationId = None;
            ReturnedAt = None;
        }
    static member NewFromReservation (reservation: Reservation) (loanedAt: DateTime) = 
        {
            LoanId = LoanId.New(); 
            BookId = reservation.BookId;
            UserId = reservation.UserId;
            LoanedAt = loanedAt;
            TimeSlot = reservation.TimeSlot;
            ReservationId = Some reservation.ReservationId;
            ReturnedAt = None;
        }
    member this.Return (dateTime: DateTime) = 
        result
            {
                do! 
                    this.ReturnedAt
                    |> Option.isSome
                    |> not
                    |> Result.ofBool "Loan is already returned"
                return { this with ReturnedAt = Some dateTime } 
            }
    member this.Id = this.LoanId.Value
    static member SnapshotsInterval = 50
    static member StorageName = "_Loan"
    static member Version = "_01"
    member this.Serialize = 
        (this, jsonOptions) |> JsonSerializer.Serialize
    static member Deserialize (data: string) =
        try
            let loan = JsonSerializer.Deserialize<Loan> (data, jsonOptions)
            Ok loan
        with
            | ex -> Error ex.Message
