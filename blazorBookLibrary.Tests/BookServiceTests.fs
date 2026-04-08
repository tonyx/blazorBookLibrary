module BookServiceTests

open System
open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open System.Threading

[<Tests>]
let tests =
    testList "books service" [
        testCase "create a book and then attach an author to it - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let userService = getUserService()
            
            let userId = registerUser "test@example.com" "Password123!"

            let author = Author.NewWithoutIsni (Name.New "John Doe")
            let addAuthor = 
                authorService.AddAuthorAsync author
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addAuthor "should be ok"

            let book = Book.New (Title.New "The Great Gatsby") [author.AuthorId] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync (book, CancellationToken.None)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk retrieveBook "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

            let userId = registerUser "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now.AddHours(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

            let reservation = Reservation.New book.BookId userId timeSlot System.DateTime.Now
            let addReservation = 
                reservationService.AddReservationAsync(reservation, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk addReservation "should be ok"

            let retrieveReservation = 
                reservationService.GetReservationAsync (reservation.ReservationId)
                |> Async.AwaitTask
                |> Async.RunSynchronously

            Expect.isOk retrieveReservation "should be ok"

            let retrieveBook =
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk retrieveBook "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.CurrentReservations |> List.contains reservation.ReservationId) "should contain the reservation"

        testCase "if a book has no reservations then you can loan it - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let reservationService = getReservationService()
            let book = Book.New (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync (book, CancellationToken.None)
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

        testCase "a book that has a loan in progress cannot be loaned again - Error" <| fun _ ->
            setUp ()
            let bookService = getBookService()
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

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New book.BookId userId1 (System.DateTime.Now) timeSlot

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

            let timeSlot2 = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan2 = Loan.New book.BookId userId2 (System.DateTime.Now) timeSlot2

            let addLoan2 = 
                loanService.AddLoanAsync (loan2, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isError addLoan2 "should be error"

        testCase "loan a book and then release the loan, the book then has no loan and is returned at something - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
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

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

        testCase "should be able to get the book details containing the loan and the reservations, which are empty for fresh book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
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
            let loan = Loan.New book.BookId userId (System.DateTime.Now) timeSlot

            let addLoan = 
                loanService.AddLoanAsync (loan, System.DateTime.Now)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addLoan "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

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

        testCase "verify that when the loan is released then the book details are always in sync - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
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
                loanService.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
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

        testCase "verify that when the loan is released then the details are always in sync 2 - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
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
                loanService.ReleaseLoanAsync (loan.LoanId, System.DateTime.Now)
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

        testCase "add multiple books and retrieve them all - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book Two") [] [] [] None  Category.Other [] (Year.New 2010) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let getAllResult = 
                (bookService :> IBookService).GetAllAsync()
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isOk getAllResult "should be ok"
            let allBooks = getAllResult |> Result.get
            Expect.equal allBooks.Length 2 "should have 2 books"

        testCase "filtering books by title - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAsync (Title.New "Wars") |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by isbn - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.New "978-3-16-148410-0" |> Result.get
            let isbn2 = Isbn.New "978-0-306-40615-7" |> Result.get
            let book1 = Book.New (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) isbn1 None
            let book2 = Book.New (Title.New "Book Two") [] [] [] None  Category.Other [] (Year.New 2010) isbn2 None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            // Partial match
            let filtered = (bookService :> IBookService).SearchByIsbnAsync (Isbn.NewInvalid "148410") |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by title and isbn - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.New "978-3-16-148410-0" |> Result.get
            let isbn2 = Isbn.New "978-0-306-40615-7" |> Result.get
            let book1 = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) isbn1 None
            let book2 = Book.New (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) isbn2 None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            // Match by title
            let filteredTitle = (bookService :> IBookService).SearchByTitleAndIsbnAsync (Title.New "Star Wars", Isbn.NewEmpty()) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filteredTitle.Length 1 "should have 1 book exactly"
            Expect.equal filteredTitle.[0].BookId book1.BookId "should be book 1"

            // Match by isbn
            let filteredIsbn = (bookService :> IBookService).SearchByTitleAndIsbnAsync (Title.New "Nothing", Isbn.NewInvalid "40615") |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filteredIsbn.Length 1 "should have 1 book exactly"
            Expect.equal filteredIsbn.[0].BookId book2.BookId "should be book 2"

        testCase "change main category of a book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let addResult = bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isOk addResult "should be ok"
            
            let result = 
                (bookService :> IBookService).ChangeMainCategoryAsync (Category.ScienceFiction, book.BookId)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk result (sprintf "should be ok but was %A" result)
            
            let freshBook = (bookService :> IBookService).GetBookAsync book.BookId |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal freshBook.MainCategory Category.ScienceFiction "should be ScienceFiction"

        testCase "add additional categories to a book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let res1 = (bookService :> IBookService).AddAdditionalCategoryAsync (Category.Fantasy, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isOk res1 (sprintf "first add should be ok but was %A" res1)
            
            let res2 = (bookService :> IBookService).AddAdditionalCategoryAsync (Category.History, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isOk res2 (sprintf "second add should be ok but was %A" res2)

            let freshBook = (bookService :> IBookService).GetBookAsync book.BookId |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal freshBook.AdditionalCategories.Length 2 (sprintf "should have 2 additional categories but has %d: %A" freshBook.AdditionalCategories.Length freshBook.AdditionalCategories)
            Expect.isTrue (freshBook.AdditionalCategories |> List.contains Category.Fantasy) "should contain Fantasy"
            Expect.isTrue (freshBook.AdditionalCategories |> List.contains Category.History) "should contain History"

        testCase "remove an additional category from a book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "Star Wars 3") [] [] [] None  Category.Other [] (Year.New 1983) (Isbn.NewEmpty()) None
            bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let res1 = (bookService :> IBookService).AddAdditionalCategoryAsync (Category.Fantasy, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isOk res1 "add should be ok"
            
            let res2 = (bookService :> IBookService).RemoveAdditionalCategoryAsync (Category.Fantasy, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isOk res2 (sprintf "remove should be ok but was %A" res2)

            let freshBook = (bookService :> IBookService).GetBookAsync book.BookId |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.isFalse (freshBook.AdditionalCategories |> List.contains Category.Fantasy) "should not contain Fantasy anymore"

        testCase "cannot add the same category twice as additional - Error" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "Star Wars 4") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            (bookService :> IBookService).AddAdditionalCategoryAsync (Category.Fantasy, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            let result = (bookService :> IBookService).AddAdditionalCategoryAsync (Category.Fantasy, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isError result "should be error"

        testCase "cannot add the main category as additional - Error" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "Star Wars 5") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            // default main category is Category.Other in Book.fs constructor
            let result = (bookService :> IBookService).AddAdditionalCategoryAsync (Category.Other, book.BookId) |> Async.AwaitTask |> Async.RunSynchronously
            Expect.isError result "should be error"

        testCase "filtering books by year (Exact) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByYearAsync (YearSearch.Exact 2000) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"

        testCase "filtering books by year (Before) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByYearAsync (YearSearch.Before 1950) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by year (After) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByYearAsync (YearSearch.After 1950) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"

        testCase "filtering books by year (Range) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book 1950") [] [] [] None  Category.Other [] (Year.New 1950) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByYearAsync (YearSearch.Range (1940, 1960)) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"

        testCase "filtering books by title and year (Exact) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndYearAsync (Title.New "Wars", YearSearch.Exact 1977) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book exactly"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by title and year (Range) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndYearAsync (Title.New "Wars", YearSearch.Range (1975, 1985)) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 2 "should have 2 books"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"

        testCase "filtering books by title and year (Before) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "A New Hope") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndYearAsync (Title.New "Star", YearSearch.Before 1980) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by title and year (After) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndYearAsync (Title.New "Wars", YearSearch.After 1978) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"

        testCase "filtering books by isbn and year (Exact) - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.New "978-3-16-148410-0" |> Result.get
            let book1 = Book.New (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) isbn1 None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByIsbnAndYearAsync (isbn1, YearSearch.Exact 2000) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by invalid isbn and year - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.NewInvalid "INVALID123"
            let book1 = Book.New (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) isbn1 None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByIsbnAndYearAsync (Isbn.NewInvalid "INVALID", YearSearch.Exact 2000) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by title, isbn and year - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let title = Title.New "Star Wars"
            let isbn = Isbn.New "978-3-16-148410-0" |> Result.get
            let year = Year.New 1977
            let book1 = Book.New title [] [] [] None  Category.Other [] year isbn None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndIsbnAndYearAsync (title, isbn, YearSearch.Exact 1977) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by categories - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New (Title.New "Book 1") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            // Main category "Other" by default
            
            let book2 = Book.New 
                            (Title.New "Book 2") [] [] [] None Category.Photography [] (Year.New 2001) (Isbn.NewEmpty()) None
            
            let book3 = Book.New 
                            (Title.New "Book 3") [] [] [] None Category.Science [Category.Photography] (Year.New 2002) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByCategoriesAsync [Category.Photography] |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 2 "should have 2 books"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book2.BookId)) "should contain book 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3 (additional category)"

        testCase "filtering books by title and categories - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let title = Title.New "Star Wars"
            let book1 = Book.New 
                            title [] [] [] None Category.ScienceFiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New 
                            title [] [] [] None Category.Fiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndCategoriesAsync (title, [Category.ScienceFiction]) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by year and categories - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New 
                            (Title.New "Star Wars") [] [] [] None Category.ScienceFiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New 
                            (Title.New "Star Wars 2") [] [] [] None Category.ScienceFiction [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByYearAndCategoriesAsync (YearSearch.Exact 1977, [Category.ScienceFiction]) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by title, year and categories - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let title = Title.New "Star Wars"
            let book1 = Book.New 
                            title [] [] [] None Category.ScienceFiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByTitleAndYearAndCategoriesAsync (title, YearSearch.Exact 1977, [Category.ScienceFiction]) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"

        testCase "filtering books by author - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New (Name.New "Author 2") (Isni.NewEmpty())
            
            (authorService :> IAuthorService).AddAuthorAsync author1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (authorService :> IAuthorService).AddAuthorAsync author2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New (Title.New "Book 1") [authorId1] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book 2") [authorId2] [] [] None  Category.Other [] (Year.New 2001) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Book 3") [authorId1; authorId2] [] [] None  Category.Other [] (Year.New 2002) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByAuthorAsync authorId1 |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 2 "should have 2 books for author 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"

        testCase "filtering books by multiple authors - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New (Name.New "Author 2") (Isni.NewEmpty())
            let author3 = Author.New (Name.New "Author 3") (Isni.NewEmpty())
            
            (authorService :> IAuthorService).AddAuthorAsync author1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (authorService :> IAuthorService).AddAuthorAsync author2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (authorService :> IAuthorService).AddAuthorAsync author3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            let authorId3 = author3.AuthorId
            
            let book1 = Book.New (Title.New "Book 1") [authorId1] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Book 2") [authorId2] [] [] None  Category.Other [] (Year.New 2001) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Book 3") [authorId3] [] [] None  Category.Other [] (Year.New 2002) (Isbn.NewEmpty()) None
            let book4 = Book.New (Title.New "Book 4") [authorId1; authorId2] [] [] None  Category.Other [] (Year.New 2003) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book4 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let filtered = (bookService :> IBookService).SearchByAuthorsAsync [authorId1; authorId2] |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 3 "should have 3 books for authors 1 and 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should contain book 4"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should not contain book 3"

        testCase "filtering books by title and multiple authors - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New (Name.New "Author 2") (Isni.NewEmpty())
            
            (authorService :> IAuthorService).AddAuthorAsync author1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (authorService :> IAuthorService).AddAuthorAsync author2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New (Title.New "Star Wars") [authorId1] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Trek") [authorId2] [] [] None  Category.Other [] (Year.New 2001) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Star Wars 2") [authorId2] [] [] None  Category.Other [] (Year.New 2002) (Isbn.NewEmpty()) None
            let book4 = Book.New (Title.New "Interstellar") [authorId1] [] [] None  Category.Other [] (Year.New 2003) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book4 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let title = Title.New "Star"
            let filtered = (bookService :> IBookService).SearchByTitleAndAuthorsAsync (title, [authorId1; authorId2]) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 3 "should have 3 books with 'Star' by authors 1 or 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book2.BookId)) "should contain book 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should not contain book 4 (Interstellar)"

        testCase "filtering books by title, multiple authors and year - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New (Name.New "Author 2") (Isni.NewEmpty())
            
            (authorService :> IAuthorService).AddAuthorAsync author1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (authorService :> IAuthorService).AddAuthorAsync author2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New (Title.New "Star Wars") [authorId1] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Trek") [authorId2] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Star Wars 2") [authorId2] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            let book4 = Book.New (Title.New "Star Wars 3") [authorId1] [] [] None  Category.Other [] (Year.New 1983) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book4 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let title = Title.New "Star"
            let yearSearch = YearSearch.After 1975
            let filtered = (bookService :> IBookService).SearchByTitleAndAuthorsAndYearAsync (title, [authorId1; authorId2], yearSearch) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 3 "should have 3 books with 'Star' by authors 1 or 2 after 1975"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book2.BookId)) "should not contain book 2 (too old)"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should contain book 4"

        testCase "filtering books by title, multiple authors, year and categories - Ok" <| fun _ ->
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New (Name.New "Author 2") (Isni.NewEmpty())
            
            (authorService :> IAuthorService).AddAuthorAsync author1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (authorService :> IAuthorService).AddAuthorAsync author2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New (Title.New "Star Wars") [authorId1] [] [] None Category.ScienceFiction [Category.Other] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New (Title.New "Star Trek") [authorId2] [] [] None Category.Drama [Category.Other] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New (Title.New "Star Wars 2") [authorId2] [] [] None Category.ScienceFiction [Category.Other] (Year.New 1980) (Isbn.NewEmpty()) None
            let book4 = Book.New (Title.New "Star Wars 3") [authorId1] [] [] None Category.History [Category.Other] (Year.New 1983) (Isbn.NewEmpty()) None
            
            (bookService :> IBookService).AddBookAsync book1 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book2 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book3 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            (bookService :> IBookService).AddBookAsync book4 |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let title = Title.New "Star"
            let yearSearch = YearSearch.After 1975
            let categories = [Category.ScienceFiction]
            let filtered = (bookService :> IBookService).SearchByTitleAndAuthorsAndYearAndCategoriesAsync (title, [authorId1; authorId2], yearSearch, categories) |> Async.AwaitTask |> Async.RunSynchronously |> Result.get
            Expect.equal filtered.Length 2 "should have 2 ScienceFiction books with 'Star' by authors 1 or 2 after 1975"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should not contain book 4 (History)"

        testCase "seal a book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "The Sealing Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let sealBook = 
                bookService.SealAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk sealBook "should be ok"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.Sealed.IsSealed(DateTime.UtcNow)) "book should be sealed (manually)"

        testCase "the business logic allow modifying a sealed book, which is prevented only at u.i. level - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let author = Author.New (Name.New "The Test Author") (Isni.NewEmpty())
            (authorService :> IAuthorService).AddAuthorAsync author |> Async.AwaitTask |> Async.RunSynchronously |> ignore
            
            let book = Book.New (Title.New "The Unmodifiable Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            bookService.AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore

            bookService.SealAsync book.BookId |> Async.AwaitTask |> Async.RunSynchronously |> ignore

            let addAuthor = 
                (bookService :> IBookService).AddAuthorToBookAsync(author.AuthorId, book.BookId)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            
            Expect.isOk addAuthor "adding author to sealed book should be ok"

        testCase "unseal a book - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "The Unsealing Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let addBook = 
                bookService.AddBookAsync book
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk addBook "should be ok"

            let sealBook = 
                bookService.SealAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk sealBook "should be ok"

            let unsealBook = 
                bookService.UnsealAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk unsealBook "should be ok"

            let retrieveBook = 
                bookService.GetBookAsync book.BookId
                |> Async.AwaitTask
                |> Async.RunSynchronously
            let bookRetrieved = retrieveBook |> Result.get
            Expect.isFalse (bookRetrieved.Sealed.IsSealed(DateTime.UtcNow)) "book should be unsealed"

        testCase "update/remove book image URL - Ok" <| fun _ ->
            setUp ()
            let bookService = getBookService()
            let book = Book.New (Title.New "The Image Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            (bookService :> IBookService).AddBookAsync book |> Async.AwaitTask |> Async.RunSynchronously |> ignore

            let imageUrl = Uri "https://example.com/cover.jpg"
            let setImageUrl = 
                (bookService :> IBookService).SetImageUrlAsync(book.BookId, imageUrl)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk setImageUrl "should set image URL ok"

            let bookAfterSet = 
                (bookService :> IBookService).GetBookAsync(book.BookId)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> Result.get
            Expect.equal bookAfterSet.ImageUrl (Some imageUrl) "image URL should be set"

            let removeImageUrl = 
                (bookService :> IBookService).RemoveImageUrlAsync(book.BookId)
                |> Async.AwaitTask
                |> Async.RunSynchronously
            Expect.isOk removeImageUrl "should remove image URL ok"

            let bookAfterRemove = 
                (bookService :> IBookService).GetBookAsync(book.BookId)
                |> Async.AwaitTask
                |> Async.RunSynchronously
                |> Result.get
            Expect.equal bookAfterRemove.ImageUrl None "image URL should be removed"
    ]

    |> testSequenced