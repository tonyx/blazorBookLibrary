
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
open Sharpino
open Sharpino.Cache

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

        testCaseTask "Add a reveiw, and retreive it verifying that the approval status is pending" <| fun _ -> 
            task {
                setUp()
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

                let! getReview = reviewService.GetReviewAsync review.ReviewId
                Expect.isOk getReview "should be ok"
                let review = getReview.OkValue
                Expect.equal review.ApprovalStatus ApprovalStatus.Pending "should be pending"
            }

        testCaseTask "Add a review, then approve it" <| fun _ -> task {
            setUp()
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

            let! getReview = reviewService.GetReviewAsync review.ReviewId
            Expect.isOk getReview "should be ok"
            let review = getReview.OkValue
            Expect.equal review.ApprovalStatus ApprovalStatus.Pending "should be pending"

            let! approveReview = reviewService.ApproveAsync review.ReviewId
            Expect.isOk approveReview "should be ok"

            let! getReviewAfterApprove = reviewService.GetReviewAsync review.ReviewId
            Expect.isOk getReviewAfterApprove "should be ok"
            let reviewAfterApprove = getReviewAfterApprove.OkValue
            Expect.isTrue reviewAfterApprove.ApprovalStatus.IsApproved "should be approved"
        }

        testCaseTask "add a review, save it as not hidden, approve it, and verify that the book details has been updated with the review" <| fun _ -> task {
            setUp()
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
            let! addLoan = (loanService:> ILoanService).AddLoanAsync (loan, (ShortLang.New "en"))
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

            let! getReview = reviewService.GetReviewAsync review.ReviewId
            Expect.isOk getReview "should be ok"
            let review = getReview.OkValue
            Expect.equal review.ApprovalStatus ApprovalStatus.Pending "should be pending"

            let! approveReview = reviewService.ApproveAsync review.ReviewId
            Expect.isOk approveReview "should be ok"

            let! bookDetail = detailsService.GetBookDetailsAsync book.BookId
            Expect.isOk bookDetail "should be ok"

            let bookDetail = bookDetail.OkValue
            Expect.equal bookDetail.ApprovedVisibleReviews.Length 1 "should have 1 review"

            let reviewContent = bookDetail.ApprovedVisibleReviews.[0].Review.Comment
            Expect.equal reviewContent "this is a very good book" "should have the same content as the review"
        }

        testCaseTask "add a review, save it as not hidden, approve it, then change is content, and verify that the details of the book has been updated accordingly " <| fun _ -> task {
            setUp()
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
            let! addLoan = (loanService:> ILoanService).AddLoanAsync (loan, (ShortLang.New "en"))
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

            let! getReview = reviewService.GetReviewAsync review.ReviewId
            Expect.isOk getReview "should be ok"
            let review = getReview.OkValue
            Expect.equal review.ApprovalStatus ApprovalStatus.Pending "should be pending"

            let! approveReview = reviewService.ApproveAsync review.ReviewId
            Expect.isOk approveReview "should be ok"

            let! bookDetail = detailsService.GetBookDetailsAsync book.BookId
            Expect.isOk bookDetail "should be ok"

            let bookDetail = bookDetail.OkValue
            Expect.equal bookDetail.ApprovedVisibleReviews.Length 1 "should have 1 review"

            let reviewContent = bookDetail.ApprovedVisibleReviews.[0].Review.Comment
            Expect.equal reviewContent "this is a very good book" "should have the same content as the review"

            let newContent = "this was a very good book"
            let! updateReview = reviewService.EditReviewAsync (review.ReviewId, newContent)
            Async.Sleep 100 |> Async.RunSynchronously

            Expect.isOk updateReview "should be ok"

            let! getReviewAfterUpdate = reviewService.GetReviewAsync review.ReviewId
            Expect.isOk getReviewAfterUpdate "should be ok"
            let reviewAfterUpdate = getReviewAfterUpdate.OkValue
            Expect.equal reviewAfterUpdate.Comment newContent "should have the same content as the review"

            let! bookDetailAfterUpdate = detailsService.GetBookDetailsAsync book.BookId
            Expect.isOk bookDetailAfterUpdate "should be ok"

            let bookDetailAfterUpdate = bookDetailAfterUpdate.OkValue
            Expect.equal bookDetailAfterUpdate.ApprovedVisibleReviews.Length 1 "should have 1 review"

            let reviewContentAfterUpdate = bookDetailAfterUpdate.ApprovedVisibleReviews.[0].Review.Comment
            Expect.equal reviewContentAfterUpdate newContent "should have the same content as the review"
        }
    ]
    |> testSequenced