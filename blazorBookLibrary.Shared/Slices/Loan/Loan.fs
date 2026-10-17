
namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System

type Loan001 =
    {
        TenantId: TenantId
        LoanId: LoanId
        BookId: BookId
        UserId: UserId
        ReservationId: Option<ReservationId>
        LoanedAt: DateTime
        TimeSlot: TimeSlot
        LoanStatus: LoanStatus
    }
    with
        member this.Upcast() : Loan =
            {
                TenantId = this.TenantId
                LoanId = this.LoanId
                BookId = this.BookId
                Patron = RegisteredUser this.UserId
                LoanedAt = this.LoanedAt
                TimeSlot = this.TimeSlot
                ReservationId = this.ReservationId
                LoanStatus = this.LoanStatus
            }

and Loan = {
    TenantId: TenantId
    LoanId: LoanId
    BookId: BookId
    Patron: Patron
    ReservationId: Option<ReservationId>
    LoanedAt: DateTime
    TimeSlot: TimeSlot
    LoanStatus: LoanStatus
} with
    static member New (tenantId: TenantId) (bookId: BookId) (userId: UserId) (loanedAt: DateTime) (timeSlot: TimeSlot) = 
        {
            TenantId = tenantId
            LoanId = LoanId.New(); 
            BookId = bookId;
            Patron = RegisteredUser userId;
            LoanedAt = loanedAt;
            TimeSlot = timeSlot;
            ReservationId = None;
            LoanStatus = InProgress;
        }
    static member NewFromReservation (reservation: Reservation) (loanedAt: DateTime) = 
        {
            TenantId = reservation.TenantId
            LoanId = LoanId.New(); 
            BookId = reservation.BookId;
            Patron = reservation.Patron;
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
                    |  _ -> Error "Loan is already returned or archived" 
                return { this with LoanStatus = Returned dateTime } 
            }

    member this.Archive (dateTime: DateTime) = 
        result
            {
                do!
                    match this.LoanStatus with
                    | InProgress -> Error "Loan is not returned yet. Please return the loan before archiving."
                    | Returned _ -> Ok ()
                    | Archived -> Error "Loan is already archived"
                return { this with LoanStatus = Archived } 
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
        with ex ->
            try 
                let loan001 = (data, jsonOptions) |> JsonSerializer.Deserialize<Loan001>
                loan001.Upcast () |> Ok
            with ex2 -> Error $"Error in deserializing {data} - {ex.Message}, {ex2.Message}"
