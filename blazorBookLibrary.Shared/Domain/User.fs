
namespace BookLibrary.Domain
open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons
open System
open System.Globalization

type User =
    {
        UserId: UserId
        Reservations: List<ReservationId>
        CurrentLoans: List<LoanId>
    }
    with
        static member New (userId: UserId) = 
            { UserId = userId; Reservations = []; CurrentLoans = [] }
    
        member this.AddReservation (reservationId: ReservationId) = 
            { this with Reservations = reservationId :: this.Reservations } |> Ok
    
        member this.RemoveReservation (reservationId: ReservationId) = 
            { this with Reservations = this.Reservations |> List.filter (fun id -> id <> reservationId) } |> Ok

        member this.AddLoan (loanId: LoanId) = 
            { this with CurrentLoans = loanId :: this.CurrentLoans } |> Ok
    
        member this.ReleaseLoan (loanId: LoanId) = 
            { this with CurrentLoans = this.CurrentLoans |> List.filter (fun id -> id <> loanId) } |> Ok

        member this.ConvertReservationToLoan (loanId: LoanId) (reservationId: ReservationId) = 
            result
                {
                    do! 
                        this.Reservations
                        |> List.contains reservationId
                        |> fun x -> if x then Ok () else Error "User has no future reservation"
                    do! 
                        this.CurrentLoans
                        |> List.contains loanId
                        |> not
                        |> fun x -> if x then Ok () else Error "User has already a current loan"
                    return
                        {
                            this with 
                                Reservations = this.Reservations |> List.filter (fun id -> id <> reservationId)
                                CurrentLoans = loanId :: this.CurrentLoans
                        }
                }

        member this.HasFutureReservation (reservationId: ReservationId) = 
            this.Reservations |> List.contains reservationId
    
        member this.HasCurrentLoan (loanId: LoanId) = 
            this.CurrentLoans |> List.contains loanId


        member this.Id = this.UserId.Value
        static member StorageName = "_User"
        static member SnapshotsInterval = 100
        static member Version = "_01"
        member this.Serialize = 
            (this, jsonOptions) |> JsonSerializer.Serialize
        static member Deserialize (json: string) = 
            try
                (json, jsonOptions) |> JsonSerializer.Deserialize<User> |> Ok
            with
                | ex -> Error (ex.Message)
    
