
module ReviewsServiceTests

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
    testList "review service tests" [
        testCaseTask "can't write a review of a book without having loaned it previously - Error" <| fun _ -> task {
            setUp ()
            let reviewService = getReviewService()
            let bookService = getBookService()
            let userService = getUserService()
            let detailsService = getDetailsService()
            let book = 
                Book.New 
                    (Title.New "The Great Gatsby") 
                    [] 
                    [] 
                    [] 
                    None  
                    Category.Other 
                    [] 
                    (Year.New 1924) 
                    (Isbn.NewEmpty()) 
                    None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! user = userService.GetUserAsync userId
            Expect.isOk user "should be ok"
            // let! userDetails = userService.GetUserDetailsAsync userId
            let! userDetails = detailsService.GetUserDetailsAsync userId
            Expect.isOk userDetails "should be ok"

            let now = DateTime.Now
            let review = 
                Review.New book.BookId userId "this is a very good book" now
            let! addReview = reviewService.AddReviewAsync review
            Expect.isError addReview "should be ok"

        }

        testCaseTask "can write a review of a book if you have loaned it previously - Ok" <| fun _ -> task {
            setUp ()
            let reviewService = getReviewService()
            let bookService = getBookService()
            let userService = getUserService()
            let detailsService = getDetailsService()
            let loanService = getLoanService()
            let book = 
                Book.New 
                    (Title.New "The Great Gatsby") 
                    [] 
                    [] 
                    [] 
                    None  
                    Category.Other 
                    [] 
                    (Year.New 1924) 
                    (Isbn.NewEmpty()) 
                    None
            let! addBook = bookService.AddBookAsync book
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! user = userService.GetUserAsync userId
            Expect.isOk user "should be ok"

            let timeSlot = 
                TimeSlot.New 
                    (DateTime.Now) 
                    (DateTime.Now.AddDays(7.0))
            let loan = 
                Loan.New 
                    book.BookId
                    userId
                    DateTime.Now 
                    timeSlot
            let! addLoan = (loanService: ILoanService).AddLoanAsync (loan, (ShortLang.New "en"))
            Expect.isOk addLoan "should be ok"

            let! releaseLoan = (loanService:> ILoanService).ReleaseLoanAsync (loan.LoanId, (ShortLang.New "en"), DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! userDetails = detailsService.GetUserDetailsAsync userId
            Expect.isOk userDetails "should be ok"

            let now = DateTime.Now
            let review = 
                Review.New book.BookId userId "this is a very good book" now
            let! addReview = reviewService.AddReviewAsync review
            Expect.isOk addReview "should be ok"
        }

    ]