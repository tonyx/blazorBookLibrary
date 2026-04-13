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

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (loan, ShortLang.New "en", System.DateTime.Now)
            Expect.isOk addLoan "should be ok"

            let! retrieveLoanResult = loanService.GetLoanAsync loan.LoanId
            Expect.isOk retrieveLoanResult "should be ok"

            let! retrieveBookResult = bookService.GetBookAsync book.BookId
            Expect.isOk retrieveBookResult "should be ok"

            let (bookRetrieved: Book) = retrieveBookResult |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoanResult |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

            let! releaseLoan = loanService.ReleaseLoanAsync (loan.LoanId, ShortLang.New "en", System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! retrieveLoanResultAfter = loanService.GetLoanAsync loan.LoanId
            Expect.isOk retrieveLoanResultAfter "should be ok"

            let! retrieveBookResultAfter = bookService.GetBookAsync book.BookId
            Expect.isOk retrieveBookResultAfter "should be ok"

            let! userRetrievedResult = userService.GetUserAsync userId
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
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"
            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (loan, ShortLang.New "en", System.DateTime.Now)
            Expect.isOk addLoan "should be ok"

            let! userRetrievedResult = userService.GetUserAsync userId
            Expect.isOk userRetrievedResult "should be ok"

            let (userRetrieved: User) = userRetrievedResult |> Result.get
            Expect.isTrue (userRetrieved.CurrentLoans |> List.contains loan.LoanId) "should contain the loan"
        }

        testCaseTask "loan a book and then release it. Verify that the book and the user don't relate to the loan anymore - Ok" <| fun _ -> task {
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"
            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (loan, ShortLang.New "en", System.DateTime.Now)
            Expect.isOk addLoan "should be ok"

            let! releaseLoan = loanService.ReleaseLoanAsync (loan.LoanId, ShortLang.New "en", System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! bookRetrievedResult = bookService.GetBookAsync book.BookId
            Expect.isOk bookRetrievedResult "should be ok"

            let! userRetrievedResult = userService.GetUserAsync userId
            Expect.isOk userRetrievedResult "should be ok"

            let (bookRetrieved: Book) = bookRetrievedResult |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (userRetrieved: User) = userRetrievedResult |> Result.get
            Expect.isFalse (userRetrieved.CurrentLoans |> List.contains loan.LoanId) "should not contain the loan"
        }
            
    ]
    |> testSequenced