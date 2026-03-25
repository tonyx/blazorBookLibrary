
namespace BookLibrary.Shared
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open System

module Details =
    open BookLibrary.Domain
    open Commons
    type BookDetails =
        { 
            Book: Book
            CurrentLoan: Option<Loan>
            FutureReservations: List<Reservation>
        }
        member this.GetNextAvailableTimeSlot (timeSlotLoanDurationInDays: int, now: DateTime)=
            let currentTimeSlots =
                (if this.CurrentLoan.IsSome then [this.CurrentLoan.Value.TimeSlot] else []) @ (this.FutureReservations |> List.map (fun reservation -> reservation.TimeSlot))
            if (currentTimeSlots.IsEmpty) then
                TimeSlot.New (now) (now + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
            else
                let maximumTimeSlot = 
                    currentTimeSlots
                    |> List.maxBy (fun timeSlot -> timeSlot.End)
                TimeSlot.New (maximumTimeSlot.End) (maximumTimeSlot.End + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
