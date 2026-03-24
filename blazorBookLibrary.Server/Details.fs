namespace BookLibrary.Details
open Sharpino.Core
open Sharpino.Cache
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open System

module Details = 
    open BookLibrary.Domain
    open BookLibrary.Shared.Commons
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

    type UserDetails2 =
        { 
            Book: Book
            FutureReservations: List<Reservation>
            CurrentLoans: List<Loan> 
        }

    type RefreshableUserDetails2 =
        {
            UserDetails: UserDetails2
            Refresher: unit -> Result<UserDetails2, string>
        }
        member this.Refresh () =
            result {
                let! UserDetails2 = this.Refresher ()
                return 
                    { 
                        this with
                            UserDetails = UserDetails2 
                    }
            }
        interface Refreshable<RefreshableUserDetails2> with
            member this.Refresh () =
                this.Refresh ()


    type BookDetails2 =
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
    
    type RefreshableBookDetails2 =
        {
            BookDetails: BookDetails2
            Refresher: unit -> Result<BookDetails2, string>
        }
        member this.Refresh () =
            result {
                let! BookDetails2 = this.Refresher ()
                return 
                    { 
                        this with
                            BookDetails = BookDetails2 
                    }
            }
        interface Refreshable<RefreshableBookDetails2> with
            member this.Refresh () =
                this.Refresh ()