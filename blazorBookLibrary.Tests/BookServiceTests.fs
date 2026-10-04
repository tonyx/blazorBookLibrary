module BookServiceTests

open System
open System.Text.Json
open TestSetup
open Expecto
open BookLibrary.Domain
open BookLibrary.Shared.Details
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open System.Threading
open Sharpino.Cache
open FsToolkit.ErrorHandling.Operator.Task

[<Tests>]
let tests =
    let timeSlotDurationInDays = 30
    let waitTime = 50
    testList "books service" [
        testCaseTask "create a book and then attach an author to it, then reserve it  - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let reservationService = getReservationService()
            let userService = getUserService()
            
            let! userId = registerUserTask "test@example.com" "Password123!"

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "John Doe")
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "should be ok"

            let book = Book.New TenantId.Default (Title.New "The Great Gatsby") [author.AuthorId] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "should be ok"

            let! retrieveBook = bookService.GetBookAsync(UserContext.Anonymous, book.BookId)
            Expect.isOk retrieveBook "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.Authors |> List.contains author.AuthorId) "should contain the author"

            let timeSlot = TimeSlot.New (System.DateTime.Now.AddHours(1)) (System.DateTime.Now.AddDays(timeSlotDurationInDays))

            let reservation = Reservation.New TenantId.Default book.BookId userId timeSlot System.DateTime.Now
            let! addReservation = reservationService.AddReservationAsync(adminContext, reservation, ShortLang.New "en")
            Expect.isOk addReservation "should be ok"

            let! retrieveReservation = reservationService.GetReservationAsync (adminContext, reservation.ReservationId)
            Expect.isOk retrieveReservation "should be ok"

            let detailsService = getDetailsService()
            let! retrieveBook2DetailsResult = detailsService.GetBookDetailsAsync(adminContext, book.BookId)
            Expect.isOk retrieveBook2DetailsResult "should be ok"

            let bookDetail = retrieveBook2DetailsResult |> Result.get
            Expect.isTrue (bookDetail.ReservationsDetails |> List.exists (fun r -> r.Reservation.ReservationId = reservation.ReservationId)) "should contain the reservation"
        }

        testCaseTask "AutocompleteAsync returns matching books with resolved authors - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()

            let author = Author.NewWithoutIsni TenantId.Default (Name.New "Arthur Conan Doyle")
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "should be ok"

            let book1 = Book.New TenantId.Default (Title.New "A Study in Scarlet") [author.AuthorId] [] [] None Category.Other [] (Year.New 1887) (Isbn.NewInvalid "1234567890") None
            let book2 = Book.New TenantId.Default (Title.New "The Sign of the Four") [author.AuthorId] [] [] None Category.Other [] (Year.New 1890) (Isbn.NewInvalid "9876543210") None
            let! add1 = bookService.AddBookAsync(adminContext, book1)
            let! add2 = bookService.AddBookAsync(adminContext, book2)
            Expect.isOk add1 "should be ok"
            Expect.isOk add2 "should be ok"

            let! autocompleteResult = bookService.AutocompleteAsync(adminContext, "Scarlet")
            Expect.isOk autocompleteResult "should be ok"
            let suggestions = autocompleteResult |> Result.get
            Expect.equal suggestions.Length 1 "should have 1 suggestion"
            Expect.equal suggestions.Head.Title "A Study in Scarlet" "title should match"
            Expect.equal suggestions.Head.Authors "Arthur Conan Doyle" "author name should be resolved"
            Expect.equal suggestions.Head.BookId book1.BookId.Value "book id should match"
        }

        testCaseTask "if a book has no reservations then you can loan it - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let reservationService = getReservationService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync (adminContext, book, CancellationToken.None)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! retrieveLoan = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoan "should be ok"

            let! bookRetrieved = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk bookRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"
        }

        testCaseTask "a book that has a loan in progress cannot be loaned again - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId1 = registerUserTask "test1@example.com" "Password123!"
            let! userId2 = registerUserTask "test2@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId1 (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! retrieveLoan = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoan "should be ok"

            let! bookRetrieved = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk bookRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

            let timeSlot2 = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan2 = Loan.New TenantId.Default book.BookId userId2 (System.DateTime.Now) timeSlot2

            let! addLoan2 = loanService.AddLoanAsync (adminContext, loan2)
            Expect.isError addLoan2 "should be error"
        }

        testCaseTask "loan a book and then release the loan, the book then has no loan and is returned at something - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! retrieveLoan = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoan "should be ok"

            let! bookRetrieved = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk bookRetrieved "should be ok"

            let (bookRetrieved: Book) = bookRetrieved |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isSome) "should contain the loan"

            let (loanRetrieved: Loan) = retrieveLoan |> Result.get
            Expect.isTrue (loanRetrieved.BookId = book.BookId) "should contain the book"

            let! releaseLoan = loanService.ReleaseLoanAsync (adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            let! retrieveLoan2 = loanService.GetLoanAsync (adminContext, loan.LoanId)
            Expect.isOk retrieveLoan2 "should be ok"

            let! bookRetrieved2 = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk bookRetrieved2 "should be ok"

            let (bookRetrieved2: Book) = bookRetrieved2 |> Result.get
            Expect.isTrue (bookRetrieved2.CurrentLoan |> Option.isNone) "should not contain the loan"

            let (loanRetrieved2: Loan) = retrieveLoan2 |> Result.get
            Expect.isTrue (loanRetrieved2.BookId = book.BookId) "should contain the book"
        }

        testCaseTask "should be able to get the book details containing the loan and the reservations, which are empty for fresh book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let detailsService = getDetailsService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! retrieveBook = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId (System.DateTime.Now) timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.CurrentLoan |> Option.isNone) "should not contain the loan"

            let! bookDetail = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"
        }

        // todo: handle the delay or force refresh dependencies
        testCaseTask "verify that when the loan is released then the book details are always in sync - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let detailsService = getDetailsService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! retrieveBook = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! bookDetail = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync (adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime

            let! bookDetail2 = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"
        }

        testCaseTask "verify that when the loan is released then the details are always in sync 2 - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let loanService = getLoanService()
            let userService = getUserService()
            let detailsService = getDetailsService()
            let book = Book.New TenantId.Default (Title.New "the constitution") [] [] [] None  Category.Other [] (Year.New 1924) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! userId = registerUserTask "test@example.com" "Password123!"

            let! retrieveBook = bookService.GetBookAsync(adminContext, book.BookId)
            Expect.isOk retrieveBook "should be ok"

            let timeSlot = TimeSlot.New (System.DateTime.Now) (System.DateTime.Now.AddDays(timeSlotDurationInDays))
            let loan = Loan.New TenantId.Default book.BookId userId System.DateTime.Now timeSlot

            let! addLoan = loanService.AddLoanAsync (adminContext, loan)
            Expect.isOk addLoan "should be ok"

            let! bookDetail = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail "should be ok"

            let (bookDetail: BookDetails) = bookDetail |> Result.get
            Expect.isTrue (bookDetail.Book.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue (bookDetail.CurrentLoan |> Option.isSome) "should contain the loan"
            Expect.isTrue ((bookDetail.CurrentLoan.Value).Loan.LoanId = loan.LoanId) "should contain the loan"
            Expect.isTrue (bookDetail.ReservationsDetails |> List.isEmpty) "should not contain reservations"

            let! releaseLoan = loanService.ReleaseLoanAsync (adminContext, loan.LoanId, System.DateTime.Now)
            Expect.isOk releaseLoan "should be ok"

            do! Async.Sleep waitTime
            let! bookDetail2 = detailsService.GetBookDetailsAsync (adminContext, book.BookId)
            Expect.isOk bookDetail2 "should be ok"

            let (bookDetail2: BookDetails) = bookDetail2 |> Result.get
            Expect.isTrue (bookDetail2.Book.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.CurrentLoan |> Option.isNone) "should not contain the loan"
            Expect.isTrue (bookDetail2.ReservationsDetails |> List.isEmpty) "should not contain reservations"
        }

        testCaseTask "add multiple books and retrieve them all - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book Two") [] [] [] None  Category.Other [] (Year.New 2010) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! getAllResult = (bookService :> IBookService).GetAllAsync(adminContext)
            
            Expect.isOk getAllResult "should be ok"
            let allBooks = getAllResult |> Result.get
            Expect.equal allBooks.Length 2 "should have 2 books"
        }

        testCaseTask "filtering books by title - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAsync(adminContext, Title.New "Wars")
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by isbn - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.New "978-3-16-148410-0" |> Result.get
            let isbn2 = Isbn.New "978-0-306-40615-7" |> Result.get
            let book1 = Book.New TenantId.Default (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) isbn1 None
            let book2 = Book.New TenantId.Default (Title.New "Book Two") [] [] [] None  Category.Other [] (Year.New 2010) isbn2 None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            // Partial match
            let! filteredResult = (bookService :> IBookService).SearchByIsbnAsync(adminContext, Isbn.NewInvalid "148410")
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by title and isbn - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.New "978-3-16-148410-0" |> Result.get
            let isbn2 = Isbn.New "978-0-306-40615-7" |> Result.get
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) isbn1 None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) isbn2 None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            // Match by title
            let! filteredTitleResult = (bookService :> IBookService).SearchByTitleAndIsbnAsync(adminContext, Title.New "Star Wars", Isbn.NewEmpty())
            let filteredTitle = filteredTitleResult |> Result.get
            Expect.equal filteredTitle.Length 1 "should have 1 book exactly"
            Expect.equal filteredTitle.[0].BookId book1.BookId "should be book 1"

            // Match by isbn
            let! filteredIsbnResult = (bookService :> IBookService).SearchByTitleAndIsbnAsync(adminContext, Title.New "Nothing", Isbn.NewInvalid "40615")
            let filteredIsbn = filteredIsbnResult |> Result.get
            Expect.equal filteredIsbn.Length 1 "should have 1 book exactly"
            Expect.equal filteredIsbn.[0].BookId book2.BookId "should be book 2"
        }

        testCaseTask "change main category of a book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let! addResult = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addResult "should be ok"
            
            let! result = (bookService :> IBookService).ChangeMainCategoryAsync(adminContext, Category.ScienceFiction, book.BookId)
            Expect.isOk result (sprintf "should be ok but was %A" result)
            
            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.equal freshBook.MainCategory Category.ScienceFiction "should be ScienceFiction"
        }

        testCaseTask "add additional categories to a book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            let! _ = bookService.AddBookAsync(adminContext, book)
            
            let! res1 = (bookService :> IBookService).AddAdditionalCategoryAsync(adminContext, Category.Fantasy, book.BookId)
            Expect.isOk res1 (sprintf "first add should be ok but was %A" res1)
            
            let! res2 = (bookService :> IBookService).AddAdditionalCategoryAsync(adminContext, Category.History, book.BookId)
            Expect.isOk res2 (sprintf "second add should be ok but was %A" res2)

            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.equal freshBook.AdditionalCategories.Length 2 (sprintf "should have 2 additional categories but has %d: %A" freshBook.AdditionalCategories.Length freshBook.AdditionalCategories)
            Expect.isTrue (freshBook.AdditionalCategories |> List.contains Category.Fantasy) "should contain Fantasy"
            Expect.isTrue (freshBook.AdditionalCategories |> List.contains Category.History) "should contain History"
        }

        testCaseTask "remove an additional category from a book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Star Wars 3") [] [] [] None  Category.Other [] (Year.New 1983) (Isbn.NewEmpty()) None
            let! _ = bookService.AddBookAsync(adminContext, book)
            
            let! res1 = (bookService :> IBookService).AddAdditionalCategoryAsync(adminContext, Category.Fantasy, book.BookId)
            Expect.isOk res1 "add should be ok"
            
            let! res2 = (bookService :> IBookService).RemoveAdditionalCategoryAsync (adminContext, Category.Fantasy, book.BookId)
            Expect.isOk res2 (sprintf "remove should be ok but was %A" res2)

            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.isFalse (freshBook.AdditionalCategories |> List.contains Category.Fantasy) "should not contain Fantasy anymore"
        }

        testCaseTask "cannot add the same category twice as additional - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Star Wars 4") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            let! _ = bookService.AddBookAsync(adminContext, book)
            
            let! _ = (bookService :> IBookService).AddAdditionalCategoryAsync (adminContext, Category.Fantasy, book.BookId)
            let! result = (bookService :> IBookService).AddAdditionalCategoryAsync (adminContext, Category.Fantasy, book.BookId)
            Expect.isError result "should be error"
        }

        testCaseTask "cannot add the main category as additional - Error" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Star Wars 5") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let! _ = bookService.AddBookAsync(adminContext, book)
            
            // default main category is Category.Other in Book.fs constructor
            let! result = (bookService :> IBookService).AddAdditionalCategoryAsync (adminContext, Category.Other, book.BookId)
            Expect.isError result "should be error"
        }

        testCaseTask "filtering books by year (Exact) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByYearAsync (adminContext, YearSearch.Exact 2000)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"
        }

        testCaseTask "filtering books by year (Before) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByYearAsync (adminContext, YearSearch.Before 1950)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by year (After) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByYearAsync (adminContext, YearSearch.After 1950)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"
        }

        testCaseTask "filtering books by year (Range) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Book 1900") [] [] [] None  Category.Other [] (Year.New 1900) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 1950") [] [] [] None  Category.Other [] (Year.New 1950) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Book 2000") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            
            let! filteredResult = (bookService :> IBookService).SearchByYearAsync (adminContext, YearSearch.Range (1940, 1960))
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"
        }

        testCaseTask "filtering books by title and year (Exact) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndYearAsync(adminContext, Title.New "Wars", YearSearch.Exact 1977)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book exactly"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by title and year (Range) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndYearAsync(adminContext, Title.New "Wars", YearSearch.Range (1975, 1985))
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 2 "should have 2 books"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
        }

        testCaseTask "filtering books by title and year (Before) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "A New Hope") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndYearAsync(adminContext, Title.New "Star", YearSearch.Before 1980)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by title and year (After) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Wars 2") [] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndYearAsync(adminContext, Title.New "Wars", YearSearch.After 1978)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book2.BookId "should be book 2"
        }

        testCaseTask "filtering books by isbn and year (Exact) - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.New "978-3-16-148410-0" |> Result.get
            let book1 = Book.New TenantId.Default (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) isbn1 None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            
            let! filteredResult = (bookService :> IBookService).SearchByIsbnAndYearAsync(adminContext, isbn1, YearSearch.Exact 2000)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by invalid isbn and year - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let isbn1 = Isbn.NewInvalid "INVALID123"
            let book1 = Book.New TenantId.Default (Title.New "Book One") [] [] [] None  Category.Other [] (Year.New 2000) isbn1 None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            
            let! filteredResult = (bookService :> IBookService).SearchByIsbnAndYearAsync(adminContext, Isbn.NewInvalid "INVALID", YearSearch.Exact 2000)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by title, isbn and year - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let title = Title.New "Star Wars"
            let isbn = Isbn.New "978-3-16-148410-0" |> Result.get
            let year = Year.New 1977
            let book1 = Book.New TenantId.Default title [] [] [] None  Category.Other [] year isbn None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndIsbnAndYearAsync(adminContext, title, isbn, YearSearch.Exact 1977)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by categories - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default (Title.New "Book 1") [] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            // Main category "Other" by default
            
            let book2 = Book.New TenantId.Default
                            (Title.New "Book 2") [] [] [] None Category.Photography [] (Year.New 2001) (Isbn.NewEmpty()) None
            
            let book3 = Book.New TenantId.Default
                            (Title.New "Book 3") [] [] [] None Category.Science [Category.Photography] (Year.New 2002) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            
            let! filteredResult = (bookService :> IBookService).SearchByCategoriesAsync(adminContext, [Category.Photography])
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 2 "should have 2 books"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book2.BookId)) "should contain book 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3 (additional category)"
        }

        testCaseTask "filtering books by title and categories - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let title = Title.New "Star Wars"
            let book1 = Book.New TenantId.Default
                            title [] [] [] None Category.ScienceFiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default
                            title [] [] [] None Category.Fiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndCategoriesAsync(adminContext, title, [Category.ScienceFiction])
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by year and categories - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book1 = Book.New TenantId.Default
                            (Title.New "Star Wars") [] [] [] None Category.ScienceFiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default
                            (Title.New "Star Wars 2") [] [] [] None Category.ScienceFiction [] (Year.New 1980) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            
            let! filteredResult = (bookService :> IBookService).SearchByYearAndCategoriesAsync(adminContext, YearSearch.Exact 1977, [Category.ScienceFiction])
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by title, year and categories - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let title = Title.New "Star Wars"
            let book1 = Book.New TenantId.Default
                            title [] [] [] None Category.ScienceFiction [] (Year.New 1977) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndYearAndCategoriesAsync(adminContext, title, YearSearch.Exact 1977, [Category.ScienceFiction])
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 1 "should have 1 book"
            Expect.equal filtered.[0].BookId book1.BookId "should be book 1"
        }

        testCaseTask "filtering books by author - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New TenantId.Default (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New TenantId.Default (Name.New "Author 2") (Isni.NewEmpty())
            
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author1)
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author2)
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New TenantId.Default (Title.New "Book 1") [authorId1] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2") [authorId2] [] [] None  Category.Other [] (Year.New 2001) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Book 3") [authorId1; authorId2] [] [] None  Category.Other [] (Year.New 2002) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            
            let! filteredResult = (bookService :> IBookService).SearchByAuthorAsync(adminContext, authorId1)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 2 "should have 2 books for author 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
        }

        testCaseTask "filtering books by multiple authors - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New TenantId.Default (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New TenantId.Default (Name.New "Author 2") (Isni.NewEmpty())
            let author3 = Author.New TenantId.Default (Name.New "Author 3") (Isni.NewEmpty())
            
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author1)
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author2)
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author3)
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            let authorId3 = author3.AuthorId
            
            let book1 = Book.New TenantId.Default (Title.New "Book 1") [authorId1] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2") [authorId2] [] [] None  Category.Other [] (Year.New 2001) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Book 3") [authorId3] [] [] None  Category.Other [] (Year.New 2002) (Isbn.NewEmpty()) None
            let book4 = Book.New TenantId.Default (Title.New "Book 4") [authorId1; authorId2] [] [] None  Category.Other [] (Year.New 2003) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book4)
            
            let! filteredResult = (bookService :> IBookService).SearchByAuthorsAsync(adminContext, [authorId1; authorId2])
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 3 "should have 3 books for authors 1 and 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should contain book 4"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should not contain book 3"
        }

        testCaseTask "filtering books by title and multiple authors - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New TenantId.Default (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New TenantId.Default (Name.New "Author 2") (Isni.NewEmpty())
            
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author1)
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author2)
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [authorId1] [] [] None  Category.Other [] (Year.New 2000) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [authorId2] [] [] None  Category.Other [] (Year.New 2001) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Star Wars 2") [authorId2] [] [] None  Category.Other [] (Year.New 2002) (Isbn.NewEmpty()) None
            let book4 = Book.New TenantId.Default (Title.New "Interstellar") [authorId1] [] [] None  Category.Other [] (Year.New 2003) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book4)
            
            let title = Title.New "Star"
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndAuthorsAsync(adminContext, title, [authorId1; authorId2])
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 3 "should have 3 books with 'Star' by authors 1 or 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book2.BookId)) "should contain book 2"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should not contain book 4 (Interstellar)"
        }

        testCaseTask "filtering books by title, multiple authors and year - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New TenantId.Default (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New TenantId.Default (Name.New "Author 2") (Isni.NewEmpty())
            
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author1)
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author2)
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [authorId1] [] [] None  Category.Other [] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [authorId2] [] [] None  Category.Other [] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Star Wars 2") [authorId2] [] [] None  Category.Other [] (Year.New 1980) (Isbn.NewEmpty()) None
            let book4 = Book.New TenantId.Default (Title.New "Star Wars 3") [authorId1] [] [] None  Category.Other [] (Year.New 1983) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book4)
            
            let title = Title.New "Star"
            let yearSearch = YearSearch.After 1975
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndAuthorsAndYearAsync(adminContext, title, [authorId1; authorId2], yearSearch)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 3 "should have 3 books with 'Star' by authors 1 or 2 after 1975"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book2.BookId)) "should not contain book 2 (too old)"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should contain book 4"
        }

        testCaseTask "filtering books by title, multiple authors, year and categories - Ok" <| fun _ -> task {
            setUp ()
            let authorService = getAuthorService()
            let bookService = getBookService()

            let author1 = Author.New TenantId.Default (Name.New "Author 1") (Isni.NewEmpty())
            let author2 = Author.New TenantId.Default (Name.New "Author 2") (Isni.NewEmpty())
            
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author1)
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author2)
            
            let authorId1 = author1.AuthorId
            let authorId2 = author2.AuthorId
            
            let book1 = Book.New TenantId.Default (Title.New "Star Wars") [authorId1] [] [] None Category.ScienceFiction [Category.Other] (Year.New 1977) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Star Trek") [authorId2] [] [] None Category.Drama [Category.Other] (Year.New 1966) (Isbn.NewEmpty()) None
            let book3 = Book.New TenantId.Default (Title.New "Star Wars 2") [authorId2] [] [] None Category.ScienceFiction [Category.Other] (Year.New 1980) (Isbn.NewEmpty()) None
            let book4 = Book.New TenantId.Default (Title.New "Star Wars 3") [authorId1] [] [] None Category.History [Category.Other] (Year.New 1983) (Isbn.NewEmpty()) None
            
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book3)
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book4)
            
            let title = Title.New "Star"
            let yearSearch = YearSearch.After 1975
            let categories = [Category.ScienceFiction]
            let! filteredResult = (bookService :> IBookService).SearchByTitleAndAuthorsAndYearAndCategoriesAsync(adminContext, title, [authorId1; authorId2], yearSearch, categories)
            let filtered = filteredResult |> Result.get
            Expect.equal filtered.Length 2 "should have 2 ScienceFiction books with 'Star' by authors 1 or 2 after 1975"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book1.BookId)) "should contain book 1"
            Expect.isTrue (filtered |> List.exists (fun b -> b.BookId = book3.BookId)) "should contain book 3"
            Expect.isFalse (filtered |> List.exists (fun b -> b.BookId = book4.BookId)) "should not contain book 4 (History)"
        }

        testCaseTask "seal a book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "The Sealing Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! sealBook = bookService.SealAsync(adminContext, book.BookId)
            Expect.isOk sealBook "should be ok"

            let! retrieveBook = bookService.GetBookAsync(adminContext, book.BookId)
            let (bookRetrieved: Book) = retrieveBook |> Result.get
            Expect.isTrue (bookRetrieved.Sealed.IsSealed(DateTime.UtcNow)) "book should be sealed (manually)"
        }

        testCaseTask "the business logic allow modifying a sealed book, which is prevented only at u.i. level - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let authorService = getAuthorService()
            let author = Author.New TenantId.Default (Name.New "The Test Author") (Isni.NewEmpty())
            let! _ = (authorService :> IAuthorService).AddAuthorAsync(adminContext, author)
            
            let book = Book.New TenantId.Default (Title.New "The Unmodifiable Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! _ = bookService.AddBookAsync(adminContext, book)

            let! _ = bookService.SealAsync(adminContext, book.BookId)

            let! addAuthor = (bookService :> IBookService).AddAuthorToBookAsync(adminContext, author.AuthorId, book.BookId)
            
            Expect.isOk addAuthor "adding author to sealed book should be ok"
        }

        testCaseTask "unseal a book - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "The Unsealing Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book)
            Expect.isOk addBook "should be ok"

            let! sealBook = bookService.SealAsync(adminContext, book.BookId)
            Expect.isOk sealBook "should be ok"

            let! unsealBook = bookService.UnsealAsync(adminContext, book.BookId)
            Expect.isOk unsealBook "should be ok"

            let! retrieveBook = bookService.GetBookAsync(adminContext, book.BookId)
            let bookRetrieved = retrieveBook |> Result.get
            Expect.isFalse (bookRetrieved.Sealed.IsSealed(DateTime.UtcNow)) "book should be unsealed"
        }

        testCaseTask "update/remove book image URL - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "The Image Book") [] [] [] None  Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! _ = (bookService :> IBookService).AddBookAsync(adminContext, book)

            let imageUrl = Uri "https://example.com/cover.jpg"
            let! setImageUrl = (bookService :> IBookService).SetImageUrlAsync(adminContext, book.BookId, imageUrl)
            Expect.isOk setImageUrl "should set image URL ok"

            let! bookAfterSetResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let bookAfterSet = bookAfterSetResult |> Result.get
            Expect.equal bookAfterSet.ImageUrl (Some imageUrl) "image URL should be set"

            let! removeImageUrl = (bookService :> IBookService).RemoveImageUrlAsync(adminContext, book.BookId)
            Expect.isOk removeImageUrl "should remove image URL ok"

            let! bookAfterRemoveResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let bookAfterRemove = bookAfterRemoveResult |> Result.get
            Expect.equal bookAfterRemove.ImageUrl None "image URL should be removed"
        }

        testCaseTask "bulk edit tags on multiple books - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            
            let book1 = Book.New TenantId.Default (Title.New "Book 1") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            
            let! add1 = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! add2 = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            Expect.isOk add1 "should add book 1 ok"
            Expect.isOk add2 "should add book 2 ok"

            let editCriteria = BulkBookEdit.Empty.SetAdditionalTagsIfCondition([BookTag "bulk-edited-tag"], true)
            let! bulkEditResult = (bookService :> IBookService).BulkEditAsync(adminContext, [book1.BookId; book2.BookId], editCriteria)
            Expect.isOk bulkEditResult (sprintf "bulk edit should be ok but was: %A" bulkEditResult)

            let! book1AfterResult = (bookService :> IBookService).GetBookAsync(adminContext, book1.BookId)
            let book1After = book1AfterResult |> Result.get
            Expect.isTrue (book1After.Tags |> List.contains (BookTag "bulk-edited-tag")) "book 1 should contain the bulk edited tag"

            let! book2AfterResult = (bookService :> IBookService).GetBookAsync(adminContext, book2.BookId)
            let book2After = book2AfterResult |> Result.get
            Expect.isTrue (book2After.Tags |> List.contains (BookTag "bulk-edited-tag")) "book 2 should contain the bulk edited tag"
        }

        testCaseTask "bulk edit tags and categories on multiple books - Ok (expected to fail)" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            
            let book1 = Book.New TenantId.Default (Title.New "Book 1") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let book2 = Book.New TenantId.Default (Title.New "Book 2") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            
            let! add1 = (bookService :> IBookService).AddBookAsync(adminContext, book1)
            let! add2 = (bookService :> IBookService).AddBookAsync(adminContext, book2)
            Expect.isOk add1 "should add book 1 ok"
            Expect.isOk add2 "should add book 2 ok"

            let editCriteria = 
                BulkBookEdit.Empty
                    .SetAdditionalTagsIfCondition([BookTag "bulk-edited-tag"], true)
                    .SetAdditionalCategoriesIfCondition([Category.Fantasy; Category.History], true)

            let! bulkEditResult = (bookService :> IBookService).BulkEditAsync(adminContext, [book1.BookId; book2.BookId], editCriteria)
            Expect.isOk bulkEditResult (sprintf "bulk edit should be ok but was: %A" bulkEditResult)

            let! book1AfterResult = (bookService :> IBookService).GetBookAsync(adminContext, book1.BookId)
            let book1After = book1AfterResult |> Result.get
            Expect.isTrue (book1After.Tags |> List.contains (BookTag "bulk-edited-tag")) "book 1 should contain the bulk edited tag"
            Expect.isTrue (book1After.AdditionalCategories |> List.contains Category.Fantasy) "book 1 should contain Category.Fantasy"

            let! book2AfterResult = (bookService :> IBookService).GetBookAsync(adminContext, book2.BookId)
            let book2After = book2AfterResult |> Result.get
            Expect.isTrue (book2After.Tags |> List.contains (BookTag "bulk-edited-tag")) "book 2 should contain the bulk edited tag"
            Expect.isTrue (book2After.AdditionalCategories |> List.contains Category.Fantasy) "book 2 should contain Category.Fantasy"
        }

        testCaseTask "UpdateSbnCodeAsync and UnsetSbnCodeAsync on BookService - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Book with SBN") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! addRes = (bookService :> IBookService).AddBookAsync(adminContext, book)
            Expect.isOk addRes "should add book ok"

            let sbn = SbnCode.NewValid "CFI0001234" |> Result.get
            let! updateRes = (bookService :> IBookService).UpdateSbnCodeAsync(adminContext, sbn, book.BookId)
            Expect.isOk updateRes "should update sbn code ok"

            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.equal freshBook.SbnCode (Some sbn) "sbn code should be updated"

            let! unsetRes = (bookService :> IBookService).UnsetSbnCodeAsync(adminContext, book.BookId)
            Expect.isOk unsetRes "should unset sbn code ok"

            let! freshBookResult2 = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook2 = freshBookResult2 |> Result.get
            Expect.equal freshBook2.SbnCode None "sbn code should be unset"
        }

        testCaseTask "BooksController UpdateSbnCodeAsync and UnsetSbnCodeAsync - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Book for Controller SBN") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! addRes = (bookService :> IBookService).AddBookAsync(adminContext, book)
            Expect.isOk addRes "should add book ok"

            let controller = BookLibrary.Controllers.BooksController(bookService, null)
            let httpContext = Microsoft.AspNetCore.Http.DefaultHttpContext()
            let identity = System.Security.Claims.ClaimsIdentity([
                System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.Value.ToString())
                System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
            ], "TestAuth")
            httpContext.User <- System.Security.Claims.ClaimsPrincipal(identity)
            controller.ControllerContext <- Microsoft.AspNetCore.Mvc.ControllerContext(HttpContext = httpContext)

            let! updateAction = controller.UpdateSbnCodeAsync(book.BookId.Value, "CFI0001234")
            Expect.isTrue (updateAction :? Microsoft.AspNetCore.Mvc.OkResult) "UpdateSbnCodeAsync should return Ok"

            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.equal freshBook.SbnCode (Some (ValidSbn "CFI0001234")) "controller should update sbn code"

            let! unsetAction = controller.UnsetSbnCodeAsync(book.BookId.Value)
            Expect.isTrue (unsetAction :? Microsoft.AspNetCore.Mvc.OkResult) "UnsetSbnCodeAsync should return Ok"

            let! freshBookResult2 = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook2 = freshBookResult2 |> Result.get
            Expect.equal freshBook2.SbnCode None "controller should unset sbn code"
        }

        testCaseTask "BooksController AddPaperAsync, AddPapersAsync and RemovePaperAsync - Ok" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let book = Book.New TenantId.Default (Title.New "Book for Controller Papers") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
            let! addRes = (bookService :> IBookService).AddBookAsync(adminContext, book)
            Expect.isOk addRes "should add book ok"

            let controller = BookLibrary.Controllers.BooksController(bookService, null)
            let httpContext = Microsoft.AspNetCore.Http.DefaultHttpContext()
            let identity = System.Security.Claims.ClaimsIdentity([
                System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, adminId.Value.ToString())
                System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")
            ], "TestAuth")
            httpContext.User <- System.Security.Claims.ClaimsPrincipal(identity)
            controller.ControllerContext <- Microsoft.AspNetCore.Mvc.ControllerContext(HttpContext = httpContext)

            let paper1 = {
                PaperId = PaperId.New()
                Title = Title.New "Paper One"
                Description = Some "Description 1"
                OptionalEmbedding = None
                Authors = []
                Categories = []
                Tags = []
            }
            let! addPaperAction = controller.AddPaperAsync(book.BookId.Value, paper1)
            Expect.isTrue (addPaperAction :? Microsoft.AspNetCore.Mvc.OkResult) "AddPaperAsync should return Ok"

            let! bookWithPaper1 = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let book1 = bookWithPaper1 |> Result.get
            Expect.equal book1.Papers.Length 1 "should have 1 paper"
            Expect.equal book1.Papers.[0].PaperId paper1.PaperId "paper id should match"

            let paper2 = {
                PaperId = PaperId.New()
                Title = Title.New "Paper Two"
                Description = Some "Description 2"
                OptionalEmbedding = None
                Authors = []
                Categories = []
                Tags = []
            }
            let paper3 = {
                PaperId = PaperId.New()
                Title = Title.New "Paper Three"
                Description = Some "Description 3"
                OptionalEmbedding = None
                Authors = []
                Categories = []
                Tags = []
            }
            let! addPapersAction = controller.AddPapersAsync(book.BookId.Value, System.Collections.Generic.List [paper2; paper3])
            Expect.isTrue (addPapersAction :? Microsoft.AspNetCore.Mvc.OkResult) "AddPapersAsync should return Ok"

            let! bookWithPapers = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let book2 = bookWithPapers |> Result.get
            Expect.equal book2.Papers.Length 3 "should have 3 papers"

            let! removePaperAction = controller.RemovePaperAsync(book.BookId.Value, paper2.PaperId.Value)
            Expect.isTrue (removePaperAction :? Microsoft.AspNetCore.Mvc.OkResult) "RemovePaperAsync should return Ok"

            let! bookAfterRemoval = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let book3 = bookAfterRemoval |> Result.get
            Expect.equal book3.Papers.Length 2 "should have 2 papers remaining"
            Expect.isFalse (book3.Papers |> List.exists (fun p -> p.PaperId = paper2.PaperId)) "removed paper should not exist"
        }

        testCaseTask "AddBookAsync with SbnCode persists SbnCode correctly upon creation" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let sbn = SbnCode.NewValid "CFI0001234" |> Result.get
            let book = 
                Book.New TenantId.Default (Title.New "Book Created With SBN") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
                |> fun b -> b.UpdateSbnCode sbn |> Result.get
            let! addRes = (bookService :> IBookService).AddBookAsync(adminContext, book)
            Expect.isOk addRes "should add book with sbn ok"

            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.equal freshBook.SbnCode (Some sbn) "sbn code should be persisted upon book creation"
        }

        testCaseTask "AddBookAsync with invalid SbnCode flagged persists InvalidSbn correctly upon creation" <| fun _ -> task {
            setUp ()
            let bookService = getBookService()
            let sbn = SbnCode.NewInvalid "INVALID_123"
            let book = 
                Book.New TenantId.Default (Title.New "Book Created With Invalid SBN") [] [] [] None Category.Other [] (Year.New 2024) (Isbn.NewEmpty()) None
                |> fun b -> b.UpdateSbnCode sbn |> Result.get
            let! addRes = (bookService :> IBookService).AddBookAsync(adminContext, book)
            Expect.isOk addRes "should add book with invalid sbn ok"

            let! freshBookResult = (bookService :> IBookService).GetBookAsync(adminContext, book.BookId)
            let freshBook = freshBookResult |> Result.get
            Expect.equal freshBook.SbnCode (Some sbn) "invalid sbn code should be persisted upon book creation"
        }

        testCase "deserialize book object and upcast" <| fun _ ->
            let json = """{"TenantId":"5a982f45-1c3a-4f7d-9a54-794ed7696f23","BookId":"3b895229-70ab-4c8e-bf4e-7e41df63a37d","Title":"The Return of the King","ImageUrl":"https://ia801601.us.archive.org/view_archive.php?archive=/25/items/m_covers_0014/m_covers_0014_62.zip\u0026file=0014624056-M.jpg","Description":"The concluding volume of The Lord of the Rings, detailing the final battles against Sauron\u0027s forces and the fate of Middle-earth.","OptionalEmbedding":null,"Availability":{"Case":"Circulating"},"DistributionPoint":null,"Authors":["e3a52abf-64b8-4e11-a3db-c337e20303df"],"Translators":[],"Languages":[],"CurrentLoan":null,"Editor":null,"MainCategory":{"Case":"Other"},"AdditionalCategories":[],"Tags":[],"Year":1955,"Isbn":{"Case":"Isbn","Fields":["9780547928234"]},"Sealed":{"DateTime":"2026-07-14T10:21:02.644649Z","Sealed":false}}"""
            let deserialized = Book.Deserialize json
            Expect.isOk deserialized (sprintf "Should deserialize and upcast successfully: %A" deserialized)
            let book = deserialized |> Result.get
            Expect.equal book.Title (Title.New "The Return of the King") "Title should match"
            Expect.equal book.Year (Year.New 1955) "Year should match"
            Expect.equal book.OptionalEmbedding None "OptionalEmbedding should be None"
            Expect.equal book.SbnCode None "SbnCode should be None after upcast"

        testCase "SbnCode.IsValid validates standard, backslash, slash, and ancient SBN codes" <| fun _ ->
            let validCodes = [
                @"IT\ICCU\LI3\0004083"
                "IT/ICCU/LI3/0004083"
                @"IT\ICCU\CFI\0001234"
                "IT/ICCU/CFI/0001234"
                "CFI0001234"
                "LI30004083"
                @"LI3\0004083"
                "LI3/0004083"
                @"it\iccu\li3\0004083"
                "it/iccu/li3/0004083"
                @"IT\ICCU\UFI\E003456"
                "UFIE003456"
            ]
            for code in validCodes do
                Expect.isTrue (SbnCode.IsValid code) (sprintf "%s should be valid" code)
                Expect.isOk (SbnCode.NewValid code) (sprintf "%s should create NewValid" code)

            let invalidCodes = [
                null
                ""
                "   "
                "INVALID"
                "12345"
                @"IT\ICCU\12\123"
                @"IT\ICCU\LI3\000408"
                @"IT\ICCU\LI3\00040830"
            ]
            for code in invalidCodes do
                Expect.isFalse (SbnCode.IsValid code) (sprintf "%A should be invalid" code)
                Expect.isError (SbnCode.NewValid code) (sprintf "%A should fail NewValid" code)
    ]

    |> testSequenced