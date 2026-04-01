
namespace BookLibrary.Shared
open Sharpino.Core
open Sharpino
open System.Text.Json
open FsToolkit.ErrorHandling
open System

module Details =
    open BookLibrary.Domain
    open Commons

    type ReservationDetails =
        { 
            Reservation: Reservation
            Book: Book
            User: User
        }

    type BookDetails =
        { 
            Authors: List<Author>
            Book: Book
            CurrentLoan: Option<Loan>
            Reservations: List<Reservation>
        }
        member this.GetNextAvailableTimeSlot (timeSlotLoanDurationInDays: int, now: DateTime)=
            let currentTimeSlots =
                (if this.CurrentLoan.IsSome then [this.CurrentLoan.Value.TimeSlot] else []) @ (this.Reservations |> List.map (fun reservation -> reservation.TimeSlot))
            if (currentTimeSlots.IsEmpty) then
                TimeSlot.New (now.AddHours(1.0)) (now.AddHours(1.0) + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
            else
                let maximumTimeSlot = 
                    currentTimeSlots
                    |> List.maxBy (fun timeSlot -> timeSlot.End)
                TimeSlot.New (maximumTimeSlot.End) (maximumTimeSlot.End + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   

    type UserDetails =
        {
            User: User
            FutureReservations: List<Reservation>
            CurrentLoans: List<Loan> 
        }
        member this.HasReservedBook (bookId: BookId) =
            this.FutureReservations |> List.exists (fun reservation -> reservation.BookId = bookId)

    type AuthorDetails = {
            Author: Author
            Books: List<Book>
        }
        with 
            member 
                this.Editable = 
                    this.Author.Editable &&
                    this.Books |> List.forall (fun book -> book.NoLoan && book.NoReservations)
            member this.UnSealable = 
                this.Books |> List.forall (fun book -> book.NoLoan && book.NoReservations)


    type AdditionalBookSearchFilter = Book -> bool