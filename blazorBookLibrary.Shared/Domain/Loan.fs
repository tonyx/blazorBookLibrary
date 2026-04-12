
namespace BookLibrary.Domain
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type Loan001 = {
    LoanId: LoanId
    BookId: BookId
    UserId: UserId
    ReservationId: Option<ReservationId>
    LoanedAt: DateTime
    TimeSlot: TimeSlot
    ReturnedAt: DateTime option
} 
    with
        member
            this.Upcast () = 
                {
                    LoanId = this.LoanId;
                    BookId = this.BookId;
                    UserId = this.UserId;
                    ReservationId = this.ReservationId;
                    LoanedAt = this.LoanedAt;
                    TimeSlot = this.TimeSlot;
                    LoanStatus = 
                        match this.ReturnedAt with
                        | None -> InProgress
                        | Some dateTime -> Returned dateTime
                }

and Loan = {
    LoanId: LoanId
    BookId: BookId
    UserId: UserId
    ReservationId: Option<ReservationId>
    LoanedAt: DateTime
    TimeSlot: TimeSlot
    LoanStatus: LoanStatus
} with
    static member New (bookId: BookId) (userId: UserId) (loanedAt: DateTime) (timeSlot: TimeSlot) = 
        {
            LoanId = LoanId.New(); 
            BookId = bookId;
            UserId = userId;
            LoanedAt = loanedAt;
            TimeSlot = timeSlot;
            ReservationId = None;
            LoanStatus = InProgress;
        }
    static member NewFromReservation (reservation: Reservation) (loanedAt: DateTime) = 
        {
            LoanId = LoanId.New(); 
            BookId = reservation.BookId;
            UserId = reservation.UserId;
            LoanedAt = loanedAt;
            TimeSlot = reservation.TimeSlot;
            ReservationId = Some reservation.ReservationId;
            LoanStatus = InProgress;
        }
    member this.DueDate = 
        this.TimeSlot.End
    member this.IsOverdue (now: DateTime)= 
        this.DueDate < now

    member this.InProgress = 
        match this.LoanStatus with
        | InProgress -> true
        | _ -> false

    member this.Return (dateTime: DateTime) = 
        result
            {
                do!
                    match this.LoanStatus with
                    | InProgress -> Ok ()
                    | Returned _ -> Error "Loan is already returned" 
                return { this with LoanStatus = Returned dateTime } 
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
            | ex -> 
                try
                    let loan001 = JsonSerializer.Deserialize<Loan001> (data, jsonOptions)
                    Ok (loan001.Upcast ())
                with
                    | ex2 -> Error (ex.Message + " " + ex2.Message)
