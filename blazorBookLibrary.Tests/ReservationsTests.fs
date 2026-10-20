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
    let timeSlotDurationInDays = 30
    let waitTime = 100 
    testList "reservation service tests" [
        testCaseTask "add an overlapping reservation and verify it will be an error - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let _ = getAuthorService()
            let reservationService = getReservationService()
            let _ = getUserService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync (adminContext, book, CancellationToken.None)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let! _ = bookService.GetBookAsync (adminContext, book.BookId, CancellationToken.None)
            let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 timeSlot System.DateTime.UtcNow

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let overlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddDays(5)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let overlappingReservation = Reservation.New TenantId.Default book.BookId userId2 overlappingTimeSlot System.DateTime.UtcNow

            let! addOverlappingReservation = reservationService.AddReservationAsync (adminContext, overlappingReservation, ShortLang.New "en")
            Expect.isError addOverlappingReservation "should be an error"
            
            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"
            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.equal bookDetail.ReservationsDetails.Length 1 "should contain one reservation"
        }

        testCaseTask "add a non overlapping reservation and verify it will be ok, expect then two reservation on that book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let _ = getAuthorService()
            let reservationService = getReservationService()
            let _ = getUserService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let! _ = bookService.GetBookAsync (adminContext, book.BookId, CancellationToken.None)
            let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

            let reservation = Reservation.New TenantId.Default book.BookId userId1 timeSlot System.DateTime.UtcNow

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let nonOverlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddDays((float)timeSlotDurationInDays + 1.0)) (System.DateTime.Now.AddDays( 2.0 * (float)timeSlotDurationInDays + 1.0))
            let nonOverlappingReservation = Reservation.New TenantId.Default book.BookId userId2 nonOverlappingTimeSlot System.DateTime.UtcNow

            let! addNonOverlappingReservation = reservationService.AddReservationAsync (adminContext, nonOverlappingReservation, ShortLang.New "en")
            Expect.isOk addNonOverlappingReservation "should be ok"

            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.equal bookDetail.ReservationsDetails.Length 2 "should contain two reservations"
        }

        testCaseTask "add and remove a reservation async - Ok " <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let _ = getAuthorService()
            let reservationService = getReservationService()
            let _ = getUserService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! _ = bookService.GetBookAsync (adminContext, book.BookId, CancellationToken.None)
            let timeSlot = TimeSlot.New (System.DateTime.Now.AddDays(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let reservation = Reservation.New TenantId.Default book.BookId userId timeSlot System.DateTime.UtcNow

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"
            
            let detailsService = getDetailsService()
            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"

            let bookDetail = bookDetailResult |> Result.get
            Expect.equal (bookDetail.ReservationsDetails |> List.length) 1 "should contain one reservation"

            let! removeReservation = reservationService.RemoveReservationAsync (adminContext, reservation.ReservationId)
            Expect.isOk removeReservation "should be ok"

            let! retrieveReservation = reservationService.GetReservationAsync (adminContext, reservation.ReservationId)
            Expect.isError retrieveReservation "should not be ok"

            let! bookDetail2Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2Result "should be ok"
            let bookDetail2 = bookDetail2Result |> Result.get
            Expect.equal (bookDetail2.ReservationsDetails |> List.length) 0 "should not contain reservations"
        }

        // todo: handle the delay needed to refresh of dependencies
        testCaseTask "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let _ = getAuthorService()
            let _ = getUserService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! _ = bookService.GetBookAsync(adminContext, book.BookId)
            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId (userId) System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId) 
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync(adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime

            let! bookDetail2Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2Result "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2Result |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            do! Async.Sleep waitTime

            let! bookDetail3Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail3Result "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3Result |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"
        }

        // todo: handle the delay related to refresh dependencies or force a refersh
        testCaseTask "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation 2" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let _ = getAuthorService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let _ = getUserService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId) 
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync(adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail2Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2Result "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2Result |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail3Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail3Result "should be ok"

            Async.Sleep 1000 |> Async.RunSynchronously
            let (bookDetail3: BookDetails) = bookDetail3Result |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"
        }

        // todo: handle the delay related to refresh dependencies or force a refersh
        testCaseTask "should be able to add a reservation to a book and then retrieve the bookdetails containing the reservation async" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let reservationService = getReservationService()
            let loanService = getLoanService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let! _ = bookService.GetBookAsync(adminContext, book.BookId)
            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId1 System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync(adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail2Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2Result "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2Result |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            do! Async.Sleep waitTime

            let! bookDetail3Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail3Result "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3Result |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"
        }

        // use the async version of this test
        ptestCaseTask "should be able to add more than one reservation to a book and then retrieve the bookdetails containing the reservations" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let reservationService = getReservationService()
            let loanService = getLoanService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"
            let! userId3 = registerUserTask "test3@example.com" "Password123!"

            let! _ = bookService.GetBookAsync(adminContext, book.BookId)
            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId1 System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync(adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail2Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2Result "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2Result |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            do! Async.Sleep waitTime

            let! bookDetail3Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail3Result "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3Result |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

            let secondFutureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(2)) (System.DateTime.Now.AddMonths(3))
            let secondReservation = Reservation.New TenantId.Default book.BookId userId3 secondFutureTimeSlot (System.DateTime.Now)

            let! addSecondReservation = reservationService.AddReservationAsync (adminContext, secondReservation, ShortLang.New "en")
            Expect.isOk addSecondReservation "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail4Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail4Result "should be ok"

            let (bookDetail4: BookDetails) = bookDetail4Result |> Result.get
            Expect.isTrue (bookDetail4.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.ReservationsDetails |> List.length = 2) "should contain the reservation"
        }
            
        testCaseTask "should be able to add more than one reservation to a book and then retrieve the bookdetails containing the reservations async - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let reservationService = getReservationService()
            let loanService = getLoanService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"
            let! userId3 = registerUserTask "test3@example.com" "Password123!"

            let! _ = bookService.GetBookAsync (adminContext, book.BookId, CancellationToken.None)
            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId1 System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync(adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail2Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2Result "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2Result |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId2 futureTimeSlot (System.DateTime.Now)

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail3Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail3Result "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3Result |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

            let secondFutureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(2)) (System.DateTime.Now.AddMonths(3))
            let secondReservation = Reservation.New TenantId.Default book.BookId userId3 secondFutureTimeSlot (System.DateTime.Now)

            let! addSecondReservation = reservationService.AddReservationAsync (adminContext, secondReservation, ShortLang.New "en")
            Expect.isOk addSecondReservation "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail4Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail4Result "should be ok"

            let (bookDetail4: BookDetails) = bookDetail4Result |> Result.get
            Expect.isTrue (bookDetail4.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail4.ReservationsDetails |> List.length = 2) "should contain the reservation"
        }

        testCaseTask "cannot add a reservation that overlaps an existing reservation " <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let reservationService = getReservationService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)

            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! bookDetail3Result = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail3Result "should be ok"

            let (bookDetail3: BookDetails) = bookDetail3Result |> Result.get
            Expect.isTrue (bookDetail3.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail3.ReservationsDetails |> List.length = 1) "should contain the reservation"

            let overlappingTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let overlappingReservation = Reservation.New TenantId.Default book.BookId userId2 overlappingTimeSlot (System.DateTime.Now)

            let! addOverlappingReservation = reservationService.AddReservationAsync (adminContext, overlappingReservation, ShortLang.New "en")
            Expect.isError addOverlappingReservation "should not be ok"
        }
            
        testCaseTask "when there is no loan and no reservation the bookDetails should return a timeSlot that starts from now and ends in 30 days" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! bookDetailResult = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetailResult "should be ok"

            let (bookDetail: BookDetails) = bookDetailResult |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let timeNow = System.DateTime.Now

            let expectedTimeSlot = TimeSlot.New (timeNow.AddHours(1.0)) (timeNow.AddHours(1.0) + TimeSpan.FromDays(float timeSlotDurationInDays))
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let actualSuggestedTimeSlot =
                bookDetail.GetNextAvailableTimeSlot(timeSlotDurationInDays, timeNow)

            Expect.equal actualSuggestedTimeSlot expectedTimeSlot "should return a timeSlot that starts from now and ends in 30 days"
        }

        testCaseTask "add a reservation and verify that the involved user has reference to that reservation - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let userService = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! userDetailsResult = userService.GetUserDetailsAsync(adminContext, userId1)
            Expect.isOk userDetailsResult "should be ok"

            Expect.isTrue (userDetailsResult.OkValue.FutureReservations |> List.length = 1) "should contain the reservation"
        }

        testCaseTask "add a reservation, then remove it. Verify that the user has no reservation anymore" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let userService = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! userDetailsResult = userService.GetUserDetailsAsync(adminContext, userId1)
            Expect.isOk userDetailsResult "should be ok"

            Expect.isTrue (userDetailsResult.OkValue.FutureReservations |> List.length = 1) "should contain the reservation"

            let! removeReservation = reservationService.RemoveReservationAsync (adminContext, reservation.ReservationId)
            Expect.isOk removeReservation "should be ok"

            let! user2DetailsResult = userService.GetUserDetailsAsync(adminContext, userId1)
            Expect.isOk user2DetailsResult "should be ok"

            Expect.isTrue (user2DetailsResult.OkValue.FutureReservations |> List.isEmpty) "should not contain the reservation"
        }

        testCaseTask "add a reservation and retrieve the reservation details" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let detailsService = getDetailsService()
            let userService = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! reservationDetailsResult = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)

            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"
        }

        testCaseTask "given a reservation transformed into a loan, if the current time is within the time slot, the loan time slot is the same of the reservation - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let detailsService = getDetailsService()
            let loanService = getLoanService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! reservationDetailsResult = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let loan = reservationDetailsResult.OkValue.ToLoan (System.DateTime.Now.AddDays(32))
            Expect.isOk loan "should be ok"
            Expect.equal loan.OkValue.BookId book.BookId "should have the same book id"
            let loanUserId =
                match loan.OkValue.Patron with
                | RegisteredUser u -> u
                | Guest _ -> failwith "unexpected guest"
            Expect.equal loanUserId userId1 "should have the same user id"
            Expect.equal loan.OkValue.TimeSlot reservation.TimeSlot "should have the same time slot"
        }

        testCaseTask "trying to transform a reservation into a loan, if the current time over the reservation time slot, then the operation fails - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let detailsService = getDetailsService()
            let loanService = getLoanService()
            let _ = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! reservationDetailsResult = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let loan = reservationDetailsResult.OkValue.ToLoan (System.DateTime.Now.AddMonths(3))
            Expect.isError loan "should be error"
        }

        testCaseTask "a reservation is expired if the current time is over the reservation time slot" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)

            let isExpired = reservation.IsExpired (System.DateTime.Now.AddMonths(3))
            Expect.isTrue isExpired "should be expired"
        }

        testCaseTask "a reservation is not expired if the current time is under the reservation time slot" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let _ = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)

            let isExpired = reservation.IsExpired (System.DateTime.Now.AddMonths(1))
            Expect.isFalse isExpired "should not be expired"
        }

        testCaseTask "when a reservation is transformed into a loan and the current time is before the reservation time slot, the loan time slot starts from now plus a minute and the duration is the same of the reservation - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let detailsService = getDetailsService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! reservationDetailsResult = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let now = System.DateTime.Now   

            let loan = reservationDetailsResult.OkValue.ToLoan now

            Expect.isOk loan "should be ok"
            Expect.equal loan.OkValue.BookId book.BookId "should have the same book id"
            let loanUserId1 =
                match loan.OkValue.Patron with
                | RegisteredUser u -> u
                | Guest _ -> failwith "unexpected guest"
            Expect.equal loanUserId1 userId1 "should have the same user id"
            Expect.equal loan.OkValue.TimeSlot (futureTimeSlot.Shift now) "should have the same time slot"
        }

        testCaseTask "transform a reservation into an actual loan and the conversion succedds if passing the right reservation code - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let detailsService = getDetailsService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! reservationDetailsResult = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let now = System.DateTime.Now   

            let loan = reservationDetailsResult.OkValue.ToLoan now

            Expect.isOk loan "should be ok"
            Expect.equal loan.OkValue.BookId book.BookId "should have the same book id"
            let loanUserId2 =
                match loan.OkValue.Patron with
                | RegisteredUser u -> u
                | Guest _ -> failwith "unexpected guest"
            Expect.equal loanUserId2 userId1 "should have the same user id"
            Expect.equal loan.OkValue.TimeSlot (futureTimeSlot.Shift now) "should have the same time slot"

            let! tranformedIntoALoan = (loanService :> ILoanService).TransformReservationIntoLoanAsync (adminContext, reservation.ReservationId, reservation.ReservationCode, now)
            Expect.isOk tranformedIntoALoan "should be ok"
        }

        testCaseTask "cannot transform a reservation into a loan if the reservation  code fails to match  - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let detailsService = getDetailsService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! reservationDetailsResult = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let now = System.DateTime.Now   

            let loan = reservationDetailsResult.OkValue.ToLoan now

            Expect.isOk loan "should be ok"
            Expect.equal loan.OkValue.BookId book.BookId "should have the same book id"
            let loanUserId3 =
                match loan.OkValue.Patron with
                | RegisteredUser u -> u
                | Guest _ -> failwith "unexpected guest"
            Expect.equal loanUserId3 userId1 "should have the same user id"
            Expect.equal loan.OkValue.TimeSlot (futureTimeSlot.Shift now) "should have the same time slot"

            let! tranformedIntoALoan = (loanService :> ILoanService).TransformReservationIntoLoanAsync (adminContext, reservation.ReservationId, ReservationCode.EmptyReservationCode, now)
            Expect.isError tranformedIntoALoan "should be error"
        }

        testCaseTask "cannot transform a reservation into a loan if the datenow exceeds the end of the reservation time slot - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let _ = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! (reservationDetailsResult: Result<ReservationDetails, string>) = (reservationService :> IReservationService).GetReservationDetailsAsync (adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let now = System.DateTime.Now.AddMonths(3)

            let! tranformedIntoALoan = (loanService :> ILoanService).TransformReservationIntoLoanAsync (adminContext, reservation.ReservationId, reservation.ReservationCode, now)
            Expect.isError tranformedIntoALoan "should be error"
        }

        testCaseTask "cannot transform a reservation into a loan if the book is already loaned" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let detailsService = getDetailsService()
            let _ = getUserService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let now = DateTime.Now
            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"
            let loan = Loan.New TenantId.Default book.BookId userId1 now (TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))) 
            let! addLoan = loanService.AddLoanAsync(adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let reservation = Reservation.New TenantId.Default book.BookId userId2 (TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))) (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync(adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! (reservationDetailsResult: Result<ReservationDetails, string>) = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId2) "should contain the user"

            let now = System.DateTime.Now.AddMonths(1)

            let! tranformedIntoALoan = (loanService :> ILoanService).TransformReservationIntoLoanAsync (adminContext, reservation.ReservationId, reservation.ReservationCode, now)
            Expect.isError tranformedIntoALoan "should be error"
            let (Error e) = tranformedIntoALoan
            Expect.equal e "Book is already loaned" "should have the correct error message"
        }

        testCaseTask "transform a reservation into an actual loan. verify that the loan is actually created, and the reservation in a different status - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()
            let detailsService = getDetailsService()

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! (reservationDetailsResult: Result<ReservationDetails, string>) = detailsService.GetReservationDetailsAsync(adminContext, reservation.ReservationId)
            Expect.isOk reservationDetailsResult "should be ok"

            Expect.isTrue (reservationDetailsResult.OkValue.Reservation.ReservationId = reservation.ReservationId) "should contain the reservation"
            Expect.isTrue (reservationDetailsResult.OkValue.Book.BookId = book.BookId) "should contain the book"
            Expect.isTrue (reservationDetailsResult.OkValue.UserDetails.Value.User.UserId = userId1) "should contain the user"

            let now = System.DateTime.Now   

            let loan = reservationDetailsResult.OkValue.ToLoan now

            Expect.isOk loan "should be ok"
            Expect.equal loan.OkValue.BookId book.BookId "should have the same book id"
            let loanUserId4 =
                match loan.OkValue.Patron with
                | RegisteredUser u -> u
                | Guest _ -> failwith "unexpected guest"
            Expect.equal loanUserId4 userId1 "should have the same user id"
            Expect.equal loan.OkValue.TimeSlot (futureTimeSlot.Shift now) "should have the same time slot"

            let! tranformedIntoALoan = (loanService :> ILoanService).TransformReservationIntoLoanAsync (adminContext, reservation.ReservationId, reservation.ReservationCode, now)
            Expect.isOk tranformedIntoALoan "should be ok"

            let! loans = loanService.GetLoansAsync(adminContext)
            Expect.isOk loans "should be ok"
            Expect.equal loans.OkValue.Length 1 "should have 1 loan"

            let! (getReservationResult: Result<Reservation, string>) = reservationService.GetReservationAsync(adminContext, reservation.ReservationId)
            Expect.isOk getReservationResult "should be ok"
            let reservation = getReservationResult.OkValue
            Expect.equal reservation.Status ReservationStatus.Loaned "should have status loaned"

            Expect.isTrue (loans.OkValue |> List.exists (fun l -> (match l.Patron with RegisteredUser u -> u | Guest _ -> failwith "unexpected guest") = userId1)) "should contain the user"
            Expect.isTrue (loans.OkValue |> List.exists (fun l -> l.BookId = book.BookId)) "should contain the book"
            Expect.isTrue (loans.OkValue |> List.exists (fun l -> l.TimeSlot = futureTimeSlot.Shift now)) "should contain the time slot"
            Expect.isTrue (loans.OkValue |> List.exists (fun l -> l.ReservationId = Some reservation.ReservationId)) "should contain the reservation id"
            Expect.isTrue (loans.OkValue |> List.exists (fun l -> l.LoanStatus.IsInProgress)) "should not be returned"

            let loan = loans.OkValue |> List.head

            let! userDetailsResult = userService.GetUserDetailsAsync(adminContext, userId1)
            Expect.isOk userDetailsResult "should be ok"
            let userDetails = userDetailsResult |> Result.get
            Expect.equal (userDetails.CurrentLoans.Length) 1 "should be 1"
            Expect.equal (userDetails.CurrentLoans |> List.head |> fst).LoanId loan.LoanId "should contain the loan"

            // let! book = bookService.GetBookAsync book.BookId
            // Expect.isOk book "should be ok"
            // let (book: Book) = book |> Result.get
            // Expect.isTrue book.NoLoan "should not be on loan"
            // Expect.equal book.CurrentReservations.Length 0 "should have 0 reservations"


        }

        testCaseTask "generate pickup PIN and transform reservation into loan by PIN - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let detailsService = getDetailsService()
            let reservationService = getReservationService()
            let loanService = getLoanService()
            let userService = getUserService()

            let book = Book.New TenantId.Default (Title.New "the pickup pin book") [] [] [] None Category.Other [] (Year.New 2026) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "testpin@example.com" "Password123!"

            let futureTimeSlot = TimeSlot.New (System.DateTime.Now.AddMonths(1)) (System.DateTime.Now.AddMonths(2))
            let reservation = Reservation.New TenantId.Default book.BookId userId1 futureTimeSlot (System.DateTime.Now)
            let! addReservation = reservationService.AddReservationAsync (adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            // 1. Generate Pickup PIN
            let! generatePinResult = (reservationService :> IReservationService).GeneratePickupPinAsync(adminContext, reservation.ReservationId)
            Expect.isOk generatePinResult "should generate PIN successfully"
            let (rawPin, expiresAt) = generatePinResult.OkValue
            Expect.equal rawPin.Length 6 "PIN should be 6 digits"

            // Verify pin has been stored as a secure hash in the reservation state
            let! getReservationResult = reservationService.GetReservationAsync(adminContext, reservation.ReservationId)
            Expect.isOk getReservationResult "should retrieve reservation state"
            let updatedReservation = getReservationResult.OkValue
            Expect.isTrue updatedReservation.PickupPinHash.IsSome "PickupPinHash should be present"
            Expect.isTrue updatedReservation.PickupPinExpiresAt.IsSome "PickupPinExpiresAt should be present"

            // Compute expected SHA-256 hash to assert security
            let pinBytes = System.Text.Encoding.UTF8.GetBytes(rawPin)
            let hashBytes = System.Security.Cryptography.SHA256.HashData(pinBytes)
            let expectedHash = System.Convert.ToHexString(hashBytes).ToLowerInvariant()
            Expect.equal updatedReservation.PickupPinHash.Value expectedHash "stored hash should match secure SHA-256 computation"

            // 2. Try to checkout by invalid PIN - should fail
            let! checkoutWithInvalidPin = (loanService :> ILoanService).TransformReservationIntoLoanByPinAsync(adminContext, reservation.ReservationId, "000000", System.DateTime.UtcNow)
            Expect.isError checkoutWithInvalidPin "should fail checkout with invalid PIN"

            // 3. Try to checkout by valid PIN - should succeed
            let! checkoutWithValidPin = (loanService :> ILoanService).TransformReservationIntoLoanByPinAsync(adminContext, reservation.ReservationId, rawPin, System.DateTime.UtcNow)
            Expect.isOk checkoutWithValidPin "should succeed checkout with valid PIN"

            // Verify checkout effects
            let! loans = loanService.GetLoansAsync(adminContext)
            Expect.isOk loans "should retrieve loans list"
            Expect.equal loans.OkValue.Length 1 "should contain exactly one loan"
            let createdLoan = loans.OkValue |> List.head
            let createdLoanUserId =
                match createdLoan.Patron with
                | RegisteredUser u -> u
                | Guest _ -> failwith "unexpected guest"
            Expect.equal createdLoanUserId userId1 "loan should belong to patron"
            Expect.equal createdLoan.BookId book.BookId "loan should contain book"

            let! getReservationAfterCheckout = reservationService.GetReservationAsync(adminContext, reservation.ReservationId)
            Expect.isOk getReservationAfterCheckout "should retrieve reservation"
            Expect.equal getReservationAfterCheckout.OkValue.Status ReservationStatus.Loaned "reservation status should transition to Loaned"
            Expect.isTrue getReservationAfterCheckout.OkValue.PickupPinHash.IsNone "PIN hash should be cleared post-checkout"
        }
        testCaseTask "in app reservation manager notification template format (en and it)" <| fun _ -> task {
            let retriever = BookLibrary.Services.InAppMessagesRetriever() :> IInAppMessagesRetriever
            let bookTitle = Title.New "The Hobbit"
            let! enResult = retriever.GetReservationManagerNotificationInAppAsync("alice", bookTitle, ShortLang.New "en")
            Expect.isOk enResult "English template retrieval should succeed"
            Expect.equal (enResult.OkValue.Trim()) "user alice has reserved the book The Hobbit" "English message format should match expected template"

            let! itResult = retriever.GetReservationManagerNotificationInAppAsync("alice", bookTitle, ShortLang.New "it")
            Expect.isOk itResult "Italian template retrieval should succeed"
            Expect.equal (itResult.OkValue.Trim()) "l'utente alice ha richiesto il libro The Hobbit" "Italian message format should match expected template"
        }
        testCaseTask "guest can reserve an available book with no existing reservations - Ok" <| fun _ -> task {
            let bookService = getBookService()
            let reservationService = getReservationService()
            let detailsService = getDetailsService()

            let book = { Book.New TenantId.Default (Title.New "Guest Book Test") [] [] [] None Category.Other [] (Year.New 2020) (Isbn.NewEmpty()) None with Availability = Availability.CirculatingIncludingGuest }
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "add book should succeed"

            let guest = GuestPatron.Create("John Guest", phoneNumber = "1234567890", email = "guest@example.com")
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(7))
            let guestReservation = Reservation.NewGuest TenantId.Default book.BookId guest futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(adminContext, guestReservation, ShortLang.New "en")
            Expect.isOk addRes "guest reservation should succeed"

            let! resDetails = detailsService.GetReservationDetailsAsync(adminContext, guestReservation.ReservationId)
            Expect.isOk resDetails "reservation details should succeed"
            Expect.isTrue resDetails.OkValue.UserDetails.IsNone "guest has no registered user details"
            Expect.equal resDetails.OkValue.BorrowerName "John Guest" "borrower name matches guest name"

            // Second reservation for guest on the same book should fail because it already has an active reservation
            let guest2 = GuestPatron.Create("Jane Guest")
            let futureSlot2 = TimeSlot.New (DateTime.UtcNow.AddDays(8)) (DateTime.UtcNow.AddDays(14))
            let guestReservation2 = Reservation.NewGuest TenantId.Default book.BookId guest2 futureSlot2 DateTime.UtcNow
            let! addRes2 = reservationService.AddReservationAsync(adminContext, guestReservation2, ShortLang.New "en")
            Expect.isError addRes2 "second guest reservation should fail"
            match addRes2 with
            | Error err -> Expect.equal err "Book already has active reservations" "should report active reservations error"
            | Ok _ -> failwith "expected error"
        }
        testCaseTask "guest reservation can be transformed into loan by PIN - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()

            let book = { Book.New TenantId.Default (Title.New "Guest Loan PIN Test") [] [] [] None Category.Other [] (Year.New 2021) (Isbn.NewEmpty()) None with Availability = Availability.CirculatingIncludingGuest }
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "add book should succeed"

            let guest = GuestPatron.Create("Alice Guest", phoneNumber = "987654321", email = "alice@guest.com")
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(10))
            let guestReservation = Reservation.NewGuest TenantId.Default book.BookId guest futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(adminContext, guestReservation, ShortLang.New "en")
            Expect.isOk addRes "guest reservation should succeed"

            let! pinResult = reservationService.GeneratePickupPinAsync(adminContext, guestReservation.ReservationId)
            Expect.isOk pinResult "PIN generation should succeed"
            let pin, _ = pinResult.OkValue

            let! transformResult = (loanService :> ILoanService).TransformReservationIntoLoanByPinAsync(adminContext, guestReservation.ReservationId, pin, DateTime.UtcNow)
            Expect.isOk transformResult "transforming guest reservation into loan by PIN should succeed"

            let! loans = loanService.GetLoansAsync(adminContext)
            Expect.isOk loans "should retrieve loans list"
            Expect.equal loans.OkValue.Length 1 "should contain exactly one loan"
            let (createdLoan: Loan) = loans.OkValue |> List.head
            Expect.equal createdLoan.BookId book.BookId "loan book id should match"
            match createdLoan.Patron with
            | Guest g -> Expect.equal g.Name "Alice Guest" "loan patron guest name should match"
            | RegisteredUser _ -> failwith "expected guest patron"

            let! getReservation = reservationService.GetReservationAsync(adminContext, guestReservation.ReservationId)
            Expect.isOk getReservation "get reservation should succeed"
            Expect.equal getReservation.OkValue.Status ReservationStatus.Loaned "reservation should transition to Loaned"
        }
        testCaseTask "guest reservation on private library fails - Error" <| fun _ -> task {
            setUp ()
            let tenantService = getTenantService ()
            let bookService = getBookService ()
            let reservationService = getReservationService ()

            let! ownerId = registerUserTask "private_owner@test.com" "Password123!"
            let ownerContext = (UserContext.Authenticated(ownerId, [ Role.Manager ]))
            let privateTenant =
                Tenant.New(ownerId, TenantName.New "Private Library Test" |> Result.get, "123 Main St", false)
            let! addTenant = tenantService.CreateTenantAsync(ownerContext, privateTenant)
            Expect.isOk addTenant "create private tenant should succeed"

            let managerContext = ownerContext.WithNewTenant(privateTenant.TenantId)
            let book = Book.New privateTenant.TenantId (Title.New "Private Book") [] [] [] None Category.Other [] (Year.New 2021) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(managerContext, book)
            Expect.isOk addBook "add book to private tenant should succeed"

            let guest = GuestPatron.Create("Sneaky Guest", email = "sneaky@example.com")
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(7))
            let guestReservation = Reservation.NewGuest privateTenant.TenantId book.BookId guest futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(UserContext.Anonymous, guestReservation, ShortLang.New "en")
            Expect.isError addRes "guest reservation on private library should fail"
            match addRes with
            | Error err -> Expect.equal err "Guest reservations and loans are only allowed for public libraries" "should report proper error"
            | Ok _ -> failwith "expected error"
        }
        testCaseTask "guest reservation on members-only circulating book fails - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService ()
            let reservationService = getReservationService ()

            // Book.New defaults to Availability.Circulating (Members Only)
            let book = Book.New TenantId.Default (Title.New "Members Only Book") [] [] [] None Category.Other [] (Year.New 2022) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "add book should succeed"

            let guest = GuestPatron.Create("Bob Guest", email = "bob@guest.com")
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(7))
            let guestReservation = Reservation.NewGuest TenantId.Default book.BookId guest futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(adminContext, guestReservation, ShortLang.New "en")
            Expect.isError addRes "guest reservation on members-only book should fail"
            match addRes with
            | Error err -> Expect.equal err "This book is not available for guest reservations" "should report proper error"
            | Ok _ -> failwith "expected error"
        }
        testCaseTask "guest reservation on geolocalized library requires location verification - missing coords Error" <| fun _ -> task {
            setUp ()
            let tenantService = getTenantService ()
            let bookService = getBookService ()
            let reservationService = getReservationService ()

            let! ownerId = registerUserTask "geoloc_owner1@test.com" "Password123!"
            let ownerContext = (UserContext.Authenticated(ownerId, [ Role.Manager ]))
            let geoTenant =
                Tenant.New(ownerId, TenantName.New "Geoloc Library Test" |> Result.get, "Duomo, Milan", true)
            let! addTenant = tenantService.CreateTenantAsync(ownerContext, geoTenant)
            Expect.isOk addTenant "create public tenant should succeed"
            let! approveResult = tenantService.SetPublicAsync(adminContext, geoTenant.TenantId)
            Expect.isOk approveResult "approve public tenant should succeed"

            // Enable geolocation on tenant: Duomo Milan (45.4642, 9.1900) with 100m radius
            let geoLoc = GeoLocSettings.CreateEnabled(45.4642, 9.1900, 100.0) |> Result.get
            let! setGeo = tenantService.SetGeoLocSettingsAsync(ownerContext, geoTenant.TenantId, geoLoc)
            Expect.isOk setGeo "enable geolocation on tenant should succeed"

            let managerContext = ownerContext.WithNewTenant(geoTenant.TenantId)
            let book = { Book.New geoTenant.TenantId (Title.New "Geoloc Guest Book") [] [] [] None Category.Other [] (Year.New 2023) (Isbn.NewEmpty()) None with Availability = Availability.CirculatingIncludingGuest }
            let! addBook = bookService.AddBookAsync(managerContext, book)
            Expect.isOk addBook "add book to geoloc tenant should succeed"

            // Guest without coordinates tries to reserve -> must fail
            let guestNoCoords = GuestPatron.Create("Remote Guest", email = "remote@example.com")
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(7))
            let guestResNoCoords = Reservation.NewGuest geoTenant.TenantId book.BookId guestNoCoords futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(UserContext.Anonymous, guestResNoCoords, ShortLang.New "en")
            Expect.isError addRes "guest reservation without coordinates should fail on geolocalized library"
            match addRes with
            | Error err -> Expect.equal err "Guest reservation requires device location verification when library geolocation is enabled" "should report location verification required"
            | Ok _ -> failwith "expected error"
        }
        testCaseTask "guest reservation on geolocalized library fails when guest is outside proximity radius - Error" <| fun _ -> task {
            setUp ()
            let tenantService = getTenantService ()
            let bookService = getBookService ()
            let reservationService = getReservationService ()

            let! ownerId = registerUserTask "geoloc_owner2@test.com" "Password123!"
            let ownerContext = (UserContext.Authenticated(ownerId, [ Role.Manager ]))
            let geoTenant =
                Tenant.New(ownerId, TenantName.New "Geoloc Library Radius Test" |> Result.get, "Duomo, Milan", true)
            let! addTenant = tenantService.CreateTenantAsync(ownerContext, geoTenant)
            Expect.isOk addTenant "create public tenant should succeed"
            let! approveResult = tenantService.SetPublicAsync(adminContext, geoTenant.TenantId)
            Expect.isOk approveResult "approve public tenant should succeed"

            // Library at (45.4642, 9.1900) with 100m radius
            let geoLoc = GeoLocSettings.CreateEnabled(45.4642, 9.1900, 100.0) |> Result.get
            let! setGeo = tenantService.SetGeoLocSettingsAsync(ownerContext, geoTenant.TenantId, geoLoc)
            Expect.isOk setGeo "enable geolocation on tenant should succeed"

            let managerContext = ownerContext.WithNewTenant(geoTenant.TenantId)
            let book = { Book.New geoTenant.TenantId (Title.New "Geoloc Guest Book 2") [] [] [] None Category.Other [] (Year.New 2023) (Isbn.NewEmpty()) None with Availability = Availability.CirculatingIncludingGuest }
            let! addBook = bookService.AddBookAsync(managerContext, book)
            Expect.isOk addBook "add book should succeed"

            // Guest ~4km away at (45.5000, 9.1900)
            let farCoords = Coordinates.New(45.5000, 9.1900)
            let guestFar = GuestPatron.Create("Far Guest", email = "far@example.com", coordinates = farCoords)
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(7))
            let guestResFar = Reservation.NewGuest geoTenant.TenantId book.BookId guestFar futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(UserContext.Anonymous, guestResFar, ShortLang.New "en")
            Expect.isError addRes "guest reservation far outside proximity radius should fail"
            match addRes with
            | Error err -> Expect.isTrue (err.Contains("requires proximity to the library")) $"unexpected error message: {err}"
            | Ok _ -> failwith "expected error"
        }
        testCaseTask "guest reservation on geolocalized library succeeds when guest is within proximity radius - Ok" <| fun _ -> task {
            setUp ()
            let tenantService = getTenantService ()
            let bookService = getBookService ()
            let reservationService = getReservationService ()

            let! ownerId = registerUserTask "geoloc_owner3@test.com" "Password123!"
            let ownerContext = (UserContext.Authenticated(ownerId, [ Role.Manager ]))
            let geoTenant =
                Tenant.New(ownerId, TenantName.New "Geoloc Library Proximity Ok" |> Result.get, "Duomo, Milan", true)
            let! addTenant = tenantService.CreateTenantAsync(ownerContext, geoTenant)
            Expect.isOk addTenant "create public tenant should succeed"
            let! approveResult = tenantService.SetPublicAsync(adminContext, geoTenant.TenantId)
            Expect.isOk approveResult "approve public tenant should succeed"

            // Library at (45.4642, 9.1900) with 100m radius
            let geoLoc = GeoLocSettings.CreateEnabled(45.4642, 9.1900, 100.0) |> Result.get
            let! setGeo = tenantService.SetGeoLocSettingsAsync(ownerContext, geoTenant.TenantId, geoLoc)
            Expect.isOk setGeo "enable geolocation on tenant should succeed"

            let managerContext = ownerContext.WithNewTenant(geoTenant.TenantId)
            let book = { Book.New geoTenant.TenantId (Title.New "Geoloc Guest Book 3") [] [] [] None Category.Other [] (Year.New 2023) (Isbn.NewEmpty()) None with Availability = Availability.CirculatingIncludingGuest }
            let! addBook = bookService.AddBookAsync(managerContext, book)
            Expect.isOk addBook "add book should succeed"

            // Guest very close at (45.46422, 9.19002) ~2.5m away
            let nearCoords = Coordinates.New(45.46422, 9.19002)
            let guestNear = GuestPatron.Create("Nearby Guest", email = "near@example.com", coordinates = nearCoords)
            let futureSlot = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(7))
            let guestResNear = Reservation.NewGuest geoTenant.TenantId book.BookId guestNear futureSlot DateTime.UtcNow

            let! addRes = reservationService.AddReservationAsync(UserContext.Anonymous, guestResNear, ShortLang.New "en")
            Expect.isOk addRes "guest reservation within proximity radius should succeed"
        }

        testCaseTask "manager converts first reservation in queue instantly to loan and second manager attempt fails" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let reservationService = getReservationService()
            let loanService = getLoanService()

            let book = Book.New TenantId.Default (Title.New "Instant Loan Book") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "add book should succeed"

            let! userId1 = registerUserTask "patron1@example.com" "Password123!"
            let! userId2 = registerUserTask "patron2@example.com" "Password123!"

            let slot1 = TimeSlot.New (DateTime.UtcNow.AddDays(1)) (DateTime.UtcNow.AddDays(5))
            let res1 = Reservation.New TenantId.Default book.BookId userId1 slot1 DateTime.UtcNow
            let! addRes1 = reservationService.AddReservationAsync (adminContext, res1, ShortLang.New "en")
            Expect.isOk addRes1 "add first reservation should succeed"

            let slot2 = TimeSlot.New (DateTime.UtcNow.AddDays(6)) (DateTime.UtcNow.AddDays(10))
            let res2 = Reservation.New TenantId.Default book.BookId userId2 slot2 DateTime.UtcNow
            let! addRes2 = reservationService.AddReservationAsync (adminContext, res2, ShortLang.New "en")
            Expect.isOk addRes2 "add second reservation should succeed"

            // res1 is first in queue
            let! isFirst1 = reservationService.IsReservationFirstInQueueAsync(adminContext, res1.ReservationId)
            Expect.isOk isFirst1 "should be ok"
            Expect.isTrue isFirst1.OkValue "res1 should be first in queue"

            // res2 is not first in queue
            let! isFirst2 = reservationService.IsReservationFirstInQueueAsync(adminContext, res2.ReservationId)
            Expect.isOk isFirst2 "should be ok"
            Expect.isFalse isFirst2.OkValue "res2 should not be first in queue"

            // Attempting to convert res2 by manager fails because it's not first in queue
            let! tryConvertRes2 = (loanService :> ILoanService).TransformReservationIntoLoanByManagerAsync(adminContext, res2.ReservationId, DateTime.UtcNow)
            Expect.isError tryConvertRes2 "converting res2 should fail because it's not first in queue"

            // First manager converts res1 -> succeeds
            let manager1Context = UserContext.Authenticated(adminId, [ Role.Admin ])
            let! convertRes1Mgr1 = (loanService :> ILoanService).TransformReservationIntoLoanByManagerAsync(manager1Context, res1.ReservationId, DateTime.UtcNow)
            Expect.isOk convertRes1Mgr1 "first manager convert should succeed"

            // Second manager attempts to convert res1 -> fails because reservation is already converted / not pending
            let manager2Context = UserContext.Authenticated(adminId, [ Role.Admin ])
            let! convertRes1Mgr2 = (loanService :> ILoanService).TransformReservationIntoLoanByManagerAsync(manager2Context, res1.ReservationId, DateTime.UtcNow)
            Expect.isError convertRes1Mgr2 "second manager convert should fail because already converted into loan"
        }
    ]
    |> testSequenced

