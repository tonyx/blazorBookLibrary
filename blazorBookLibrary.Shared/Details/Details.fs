
namespace BookLibrary.Shared
open System

module Details =
    open BookLibrary.Domain
    open Commons
    open blazorBookLibrary.Data
    open System.Threading.Tasks

    type UserDetails =
        {
            User: User
            ApplicationUser: ApplicationUser
            FutureReservations: List<Reservation*Book>
            CurrentLoans: List<Loan * Book> 
            BooksAndReviews: List<Book * Review>
        }
        member this.HasReservedBook (bookId: BookId) =
            this.FutureReservations |> List.exists (fun (reservation, _) -> reservation.BookId = bookId)

    type ReservationDetails =
        { 
            Reservation: Reservation
            Book: Book
            UserDetails: UserDetails
        }
        member this.ToLoan (now: DateTime) = 
            if now > this.Reservation.TimeSlot.End then
                Error "Reservation time slot has expired"
            else
                let loan =
                    Loan.NewFromReservation (this.Reservation) now
                if (now > this.Reservation.TimeSlot.Start) then
                    loan |> Ok
                else
                    { loan with TimeSlot = this.Reservation.TimeSlot.Shift now } |> Ok

    type LoanDetails =
        {
            Loan: Loan
            Book: Book
            UserDetails: UserDetails
        }

    type BookDetails =
        { 
            Authors: List<Author>
            Book: Book
            CurrentLoan: Option<LoanDetails>
            ReservationsDetails: List<ReservationDetails>
        }
        member this.GetNextAvailableTimeSlot (timeSlotLoanDurationInDays: int, now: DateTime)=
            let currentTimeSlots =
                (if this.CurrentLoan.IsSome then [this.CurrentLoan.Value.Loan.TimeSlot] else []) @ (this.ReservationsDetails |> List.map (fun reservationDetails -> reservationDetails.Reservation.TimeSlot))
            if (currentTimeSlots.IsEmpty) then
                TimeSlot.New (now.AddHours(1.0)) (now.AddHours(1.0) + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
            else
                let maximumTimeSlot = 
                    currentTimeSlots
                    |> List.maxBy (fun timeSlot -> timeSlot.End)
                TimeSlot.New (maximumTimeSlot.End) (maximumTimeSlot.End + TimeSpan.FromDays(float timeSlotLoanDurationInDays))   
        member this.PendingReservationsDetails: List<ReservationDetails> =
            this.ReservationsDetails 
            |> List.filter (fun reservationDetails -> reservationDetails.Reservation.Status = ReservationStatus.Pending)

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