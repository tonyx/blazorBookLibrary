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
    testList "loan service tests" [
        testCase "loan a book and then release the loan, the book then has no loan and is returned at something, use async - Ok" <| fun _ ->
            setUp ()
            let loanService = getLoanService()
            let bookService = getBookService()
            let userService = getUserService()
            let userId = registerUser "test@example.com" "Password123!"

            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let retrieveLoan = 
                loanService.GetLoanAsync loan.LoanId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveLoan "should be ok"

            let bookRetrieved = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

            let releaseLoan = 
                loanService.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk releaseLoan "should be ok"

            let retrieveLoan = 
                loanService.GetLoanAsync loan.LoanId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveLoan "should be ok"

            let bookRetrieved = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let userRetrieved = 
                userService.GetUserAsync userId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk userRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        testCase "loan a book and verify that the user has that loan - Ok" <| fun _ ->
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let userId = registerUser "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let userRetrieved = 
                userService.GetUserAsync userId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk userRetrieved "should be ok"

            let (userRetrieved: User) = userRetrieved |> Result.get
            Expect.isTrue (userRetrieved.CurrentLoans |> List.contains loan.LoanId) "should contain the loan"

        testCase "loan a book and then release it. Verify that the book and the user don't relate to the loan anymore - Ok" <| fun _ ->
            setUp ()
            let loanService = getLoanService()
            let userService = getUserService()
            let bookService = getBookService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"
            let userId = registerUser "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let releaseLoan = 
                loanService.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk releaseLoan "should be ok"

            let bookRetrieved = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk bookRetrieved "should be ok"

            let userRetrieved = 
                userService.GetUserAsync userId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk userRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (userRetrieved: User) = userRetrieved |> Result.get
            Expect.isFalse (userRetrieved.CurrentLoans |> List.contains loan.LoanId) "should not contain the loan"
            
    ]