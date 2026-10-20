module LoanServiceTests

open System
open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons
open System.Threading
open blazorBookLibrary.Data
open Microsoft.AspNetCore.Identity

[<Tests>]
let tests =
    let timeSlotDurationInDays = 30
    testList "loan service tests" [
        testCaseTask "loan a book and then release the loan, the book then has no loan and is returned at something, use async - Ok" <| fun _ -> task {
            setUp ()
            let loanService = getLoanService()
            let bookService = getBookService()
            let userService = getUserService()
            let! userId = registerUserTask "test@example.com" "Password123!"

            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! retrieveLoanResult = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoanResult "should be ok"

            let! retrieveBookResult = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk retrieveBookResult "should be ok"

            let (bookRetrieved: Book) = retrieveBookResult |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoanResult |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

            let! releaseLoan = loanService.ReleaseLoanAsync (adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! retrieveLoanResultAfter = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoanResultAfter "should be ok"

            let! retrieveBookResultAfter = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk retrieveBookResultAfter "should be ok"

            let! userRetrievedResult = userService.GetUserAsync(adminContext, userId)
            Expect.isOk userRetrievedResult "should be ok"

            let (bookRetrieved: Book) = retrieveBookResultAfter |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (loanRetrieved: Loan) = retrieveLoanResultAfter |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"
        }

        testCaseTask "loan a book and verify that the user has that loan - Ok" <| fun _ -> task {
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"
            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! userDetailsResult = userService.GetUserDetailsAsync(adminContext, userId)
            Expect.isOk userDetailsResult "should be ok"

            let userDetails = userDetailsResult |> Result.get
            Expect.isTrue (userDetails.CurrentLoans |> List.exists (fun (l, _) -> l.LoanId = loan.LoanId)) "should contain the loan"
        }

        testCaseTask "loan a book and then release it. Verify that the book and the user don't relate to the loan anymore - Ok" <| fun _ -> task {
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"
            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! releaseLoan = loanService.ReleaseLoanAsync (adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! bookRetrievedResult = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk bookRetrievedResult "should be ok"

            let! userDetailsResult = userService.GetUserDetailsAsync(adminContext, userId)
            Expect.isOk userDetailsResult "should be ok"

            let (bookRetrieved: Book) = bookRetrievedResult |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let userDetails = userDetailsResult |> Result.get
            Expect.isFalse (userDetails.CurrentLoans |> List.exists (fun (l, _) -> l.LoanId = loan.LoanId)) "should not contain the loan"
        }

        testCaseTask "loan a book and then release it. the book is in the history of loans - Ok" <| fun _ -> task {
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"
            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! releaseLoan = loanService.ReleaseLoanAsync (adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! bookRetrievedResult = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk bookRetrievedResult "should be ok"

            let! userRetrievedResult = userService.GetUserAsync(adminContext, userId)
            Expect.isOk userRetrievedResult "should be ok"

            let! loanHistoryResult = loanService.GetHistoryLoansOfUserAsync (adminContext, userId)
            Expect.isOk loanHistoryResult "should be ok"

            let (loanHistory: list<Loan>) = loanHistoryResult |> Result.get
            Expect.isTrue (loanHistory |> List.exists (fun l -> l.LoanId = loan.LoanId)) "should contain the loan"
        }
        testCaseTask "add guest loan instantly without reservation - Ok" <| fun _ -> task {
            setUp ()
            let loanService = getLoanService()
            let bookService = getBookService()

            let book = { Book.New TenantId.Default (Title.New "guest loan instant test") [] [] [] None Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None with Availability = Availability.CirculatingIncludingGuest }
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should add book ok"

            let guest = GuestPatron.Create("Instant Guest", phoneNumber = "555-1234", email = "guest@instant.com")
            let timeSlot = TimeSlot.New (DateTime.UtcNow) (DateTime.UtcNow.AddDays(float timeSlotDurationInDays))
            let loan = Loan.NewGuest TenantId.Default book.BookId guest DateTime.UtcNow timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should add guest loan ok"

            let! retrieveLoanResult = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoanResult "should retrieve guest loan ok"
            let retrievedLoan = retrieveLoanResult |> Result.get
            match retrievedLoan.Patron with
            | Guest g -> Expect.equal g.Name "Instant Guest" "guest name should match"
            | RegisteredUser _ -> failwith "expected guest patron"

            let! retrieveBookResult = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk retrieveBookResult "should get book ok"
            let bookRetrieved = retrieveBookResult |> Result.get
            Expect.equal bookRetrieved.CurrentLoan (Some loan.LoanId) "book should have current loan"
        }
    ]
    |> testSequenced