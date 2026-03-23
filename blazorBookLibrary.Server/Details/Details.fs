namespace BookLibrary.Details
open Sharpino.Core
open Sharpino.Cache
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open System

module Details = 
    open BookLibrary.Domain
    open BookLibrary.Commons
    type UserDetails = 
        { 
            Book: Book
            FutureReservations: List<Reservation>
            CurrentLoans: List<Loan> 
            Refresher: unit -> Result<Book * List<Reservation> * List<Loan>, string>
        }
        member this.Refresh () =
            result {
                let! Book, FutureReservations, CurrentLoans = this.Refresher ()
                return 
                    { 
                        this with
                            Book = Book; 
                            FutureReservations = FutureReservations; 
                            CurrentLoans = CurrentLoans 
                    }
            }
        interface Refreshable<UserDetails> with
            member this.Refresh () =
                this.Refresh ()

    type BookDetails =
        { 
            Book: Book
            CurrentLoan: Option<Loan>
            FutureReservations: List<Reservation>
            Refresher: unit -> Result<Book * Option<Loan> * List<Reservation>, string>
        }
        member this.Refresh () =
            result {
                let! Book, CurrentLoan, FutureReservations = this.Refresher ()
                return 
                    { 
                        this with
                            Book = Book; 
                            CurrentLoan = CurrentLoan; 
                            FutureReservations = FutureReservations 
                    }
            }
        // member this.GetNextAvailableTimeSlot (timeSlotLoanDurationInDays: int) (now: DateTime)=
        //     let currentTimeSlots =
        //         (if this.CurrentLoan.IsSome then [this.CurrentLoan.Value.TimeSlot] else []) @ (this.FutureReservations |> List.map (fun reservation -> reservation.TimeSlot))
        //     if (currentTimeSlots.IsEmpty) then
        //         TimeSlot.New (now) (now + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
        //     else
        //         let maximumTimeSlot = 
        //             currentTimeSlots
        //             |> List.maxBy (fun timeSlot -> timeSlot.End)
        //         TimeSlot.New (maximumTimeSlot.End) (maximumTimeSlot.End + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
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

        interface Refreshable<BookDetails> with
            member this.Refresh () =
                this.Refresh ()