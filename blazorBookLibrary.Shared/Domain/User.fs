
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
        FutureReservations: List<ReservationId>
        CurrentLoans: List<LoanId>
    }
    with
        static member New (userId: UserId) = 
            { UserId = userId; FutureReservations = []; CurrentLoans = [] }
    
        member this.AddFutureReservation (reservationId: ReservationId) = 
            { this with FutureReservations = reservationId :: this.FutureReservations } |> Ok
    
        member this.RemoveFutureReservation (reservationId: ReservationId) = 
            { this with FutureReservations = this.FutureReservations |> List.filter (fun id -> id <> reservationId) } |> Ok

        member this.AddLoan (loanId: LoanId) = 
            { this with CurrentLoans = loanId :: this.CurrentLoans } |> Ok
    
        member this.ReleaseLoan (loanId: LoanId) = 
            { this with CurrentLoans = this.CurrentLoans |> List.filter (fun id -> id <> loanId) } |> Ok

        member this.LoanFromReservation (loanId: LoanId) (reservationId: ReservationId) = 
            result
                {
                    do! 
                        this.FutureReservations
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
                                FutureReservations = this.FutureReservations |> List.filter (fun id -> id <> reservationId)
                                CurrentLoans = loanId :: this.CurrentLoans
                        }
                }

        member this.HasFutureReservation (reservationId: ReservationId) = 
            this.FutureReservations |> List.contains reservationId
    
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
                json |> JsonSerializer.Deserialize<User> |> Ok
            with
                | ex -> Error (ex.Message)
    
