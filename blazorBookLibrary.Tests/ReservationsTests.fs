module ReservationsTests

open System
open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons
open System.Threading
open BookLibrary.Details.Details
open BookLibrary.Shared.Services

[<Tests>]
let tests =
    testList "reservation service tests" [
        testCase "add an overlapping reservation and verify it will be an error - Error" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let userService = getUserService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync (book, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

            let reservation = Reservation.New book.BookId userId1 timeSlot System.DateTime.UtcNow

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let overlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddDays(5)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let overlappingReservation = Reservation.New book.BookId userId2 overlappingTimeSlot System.DateTime.UtcNow

            let addOverlappingReservation = 
                reservationService.AddReservationAsync (overlappingReservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isError addOverlappingReservation "should be an error"
            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"
            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.equal bookDetail.ReservationsDetails.Length 1 "should contain one reservation"

        testCase "add a non overlapping reservation and verify it will be ok, expect then two reservation on that book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let userService = getUserService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

            let reservation = Reservation.New book.BookId userId1 timeSlot System.DateTime.UtcNow

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let nonOverlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddDays((float)timeSlotDurationInDays + 1.0)) (System.DateTime.Now.AddDays( 2.0 * (float)timeSlotDurationInDays + 1.0))
            let nonOverlappingReservation = Reservation.New book.BookId userId2 nonOverlappingTimeSlot System.DateTime.UtcNow

            let addNonOverlappingReservation = 
                reservationService.AddReservationAsync (nonOverlappingReservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addNonOverlappingReservation "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.equal bookDetail.ReservationsDetails.Length 2 "should contain two reservations"

        testCase "add and remove a reservation async - Ok " <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let userService = getUserService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId = registerUser "test@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

            let reservation = Reservation.New book.BookId userId timeSlot System.DateTime.UtcNow

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk addReservation "should be ok"
            let bookRetrieved = 
                bookService.GetBookAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.equal (bookRetrieved.CurrentReservations |> List.length) 1 "should contain one reservation"

            let removeReservation = 
                reservationService.RemoveReservationAsync (reservation.ReservationId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk removeReservation "should be ok"

            let retrieveReservation = 
                reservationService.GetReservationAsync (reservation.ReservationId)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isError retrieveReservation "should not be ok"

            let bookRetrieved2 = 
                bookService.GetBookAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved2 "should be ok"
            let (bookRetrieved2: Book) = bookRetrieved2 |> Result.get
            Expect.equal (bookRetrieved2.CurrentReservations |> List.length) 0 "should not contain reservations"

        testCase "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let userService = getUserService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId = registerUser "test@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId (userId) System.DateTime.Now timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId 
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let releaseLoan = 
                loanService.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk releaseLoan "should be ok"

            let bookDetail2 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let bookDetail3 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail3 "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3 |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

        testCase "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation 2" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId = registerUser "test@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId System.DateTime.Now timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId 
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let releaseLoan = 
                loanService.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk releaseLoan "should be ok"

            let bookDetail2 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let bookDetail3 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail3 "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3 |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

        testCase "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation async" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId1 System.DateTime.Now timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let releaseLoan = 
                loanService.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk releaseLoan "should be ok"

            let bookDetail2 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let bookDetail3 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk bookDetail3 "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3 |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

        testCase "should be able to add more than one reservation to a book and then retrieve the bookdetails containing the reservations" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let userId3 = registerUser "test3@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId1 System.DateTime.Now timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let releaseLoan = 
                loanService.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk releaseLoan "should be ok"

            let bookDetail2 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let bookDetail3 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail3 "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3 |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

            let secondFutureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(2)) (System.DateTime.Now.AddMonths(3))
            let secondReservation = Reservation.New book.BookId userId3 secondFutureTimeSlot (System.DateTime.Now)

            let addSecondReservation = 
                reservationService.AddReservationAsync (secondReservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addSecondReservation "should be ok"

            let bookDetail4 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail4 "should be ok"

            let (bookDetail4: BookDetails) = bookDetail4 |> Result.get
            Expect.isTrue (bookDetail4.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.ReservationsDetails |> List.length = 2) "should contain the reservation"
            
        testCase "should be able to add more than one reservation to a book and then retrieve the bookdetails containing the reservations async - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let userId3 = registerUser "test3@example.com" "Password123!"

            let retrieveBook = 
                bookService.GetBookAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId1 System.DateTime.Now timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan.Value.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let releaseLoan = 
                loanService.ReleaseLoanAsync(loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk releaseLoan "should be ok"

            let bookDetail2 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let bookDetail3 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail3 "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3 |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

            let secondFutureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(2)) (System.DateTime.Now.AddMonths(3))
            let secondReservation = Reservation.New book.BookId userId3 secondFutureTimeSlot (System.DateTime.Now)

            let addSecondReservation = 
                reservationService.AddReservationAsync (secondReservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addSecondReservation "should be ok"

            let bookDetail4 = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail4 "should be ok"

            let (bookDetail4: BookDetails) = bookDetail4 |> Result.get
            Expect.isTrue (bookDetail4.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.ReservationsDetails |> List.length = 2) "should contain the reservation"

        testCase "cannot add a reservation that overlaps an existing reservation " <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let userId2 = registerUser "test2@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId1 futureTimeSlot (System.DateTime.Now)

            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let bookDetail3 = 
                bookService.GetBookDetailsAsync (book.BookId, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail3 "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3 |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

            let overlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let overlappingReservation = Reservation.New book.BookId userId2 overlappingTimeSlot (System.DateTime.Now)

            let addOverlappingReservation = 
                reservationService.AddReservationAsync (overlappingReservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isError addOverlappingReservation "should not be ok"
            
        testCase "when there is no loan and no reservation the bookDetails should return a timeSlot that starts from now and ends in 30 days" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let bookDetail = 
                bookService.GetBookDetailsAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let timeNow = System.DateTime.Now

            let expectedTimeSlot = TimeSlot.New (timeNow.AddHours(1.0)) (timeNow.AddHours(1.0) + TimeSpan.FromDays(float timeSlotDurationInDays))
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let actualSuggestedTimeSlot =
                bookDetail.GetNextAvailableTimeSlot(timeSlotDurationInDays, timeNow)

            Expect.equal actualSuggestedTimeSlot expectedTimeSlot "should return a timeSlot that starts from now and ends in 30 days"

        testCase "add a reservation and verify that the involved user has reference to that reservation - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let user = 
                userService.GetUserAsync userId1
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk user "should be ok"

            Expect.isTrue (user.OkValue.Reservations |> List.length = 1) "should contain the reservation"

        testCase "add a reservation, then remove it. Verify that the user has no reservation anymore" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let user = 
                userService.GetUserAsync userId1
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk user "should be ok"

            Expect.isTrue (user.OkValue.Reservations |> List.length = 1) "should contain the reservation"

            let removeReservation = 
                reservationService.RemoveReservationAsync (reservation.ReservationId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk removeReservation "should be ok"

            let user = 
                userService.GetUserAsync userId1
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk user "should be ok"

            Expect.isTrue (user.OkValue.Reservations |> List.isEmpty) "should not contain the reservation"

        testCase "add a reservation and retrieve the reservation details" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let userService = getUserService()

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let userId1 = registerUser "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let addReservation = 
                reservationService.AddReservationAsync (reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addReservation "should be ok"

            let reservationDetails = 
                (reservationService :> IReservationService).GetReservationDetailsAsync reservation.ReservationId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk reservationDetails "should be ok"

            Expect.isTrue (reservationDetails.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetails.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetails.OkValue.UserDetails.User.UserId = userId1) "should contain the user"

    ]
    |> testSequenced
