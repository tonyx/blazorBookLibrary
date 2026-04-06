
module IsbnRegistryTests

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
    testList "isbn registry service" [
        testCase "isbn registry must be empty" <| fun _ ->
            setUp ()
            let isbnRegistry = IsbnRegistry.New()
            Expect.isTrue (isbnRegistry.Isbns |> Map.isEmpty) "should be empty"

        testCase "on the empty isbn, add a book and the book registry contains one new book" <| fun _ ->
            setUp ()
            let isbnRegistry = IsbnRegistry.New()
            let title = Title.New("Il nome della Rosa")
            let (Ok isbn) = Isbn.New("978-8804668237") 
            let bookId = BookId.New()
            let bookTitleAndId = 
                {
                    Title = title
                    BookId = bookId
                }
            let addBookToIsbBnRegistry =
                isbnRegistry.AddIsbn isbn bookTitleAndId |> Result.get
            Expect.isFalse (addBookToIsbBnRegistry.Isbns |> Map.isEmpty) "should be empty"
            Expect.isTrue (addBookToIsbBnRegistry.GetAllIsbn().Contains isbn) "should contain isbn"

        // FOCUS: THIS IS FOR A FEATURE THAT HAS BEEN PROBABLY DITCHED
        ptestCase "we have two copies of the same book with the same isdn, and we add the same book twice, the number of keys stay the same - ok" <| fun _ ->
            setUp ()
            let isbnRegistry = IsbnRegistry.New()
            let title = Title.New("Il nome della Rosa")
            let (Ok isbn) = Isbn.New("978-8804668237") 
            let bookId1 = BookId.New()
            let bookId2 = BookId.New()
            let bookTitleAndId1 =
                {
                    Title = title
                    BookId = bookId1
                }
            let bookTitleAndId2 =
                {
                    Title = title
                    BookId = bookId2
                }
            let addBookToIsbBnRegistry =
                isbnRegistry.AddIsbn isbn bookTitleAndId1 |> Result.get
            let addBookToIsbBnRegistry2 =
                addBookToIsbBnRegistry.AddIsbn isbn bookTitleAndId2 |> Result.get
            Expect.isFalse (addBookToIsbBnRegistry.Isbns |> Map.isEmpty) "should be empty"
            let bookEntries = addBookToIsbBnRegistry2.Isbns.[isbn]
            Expect.equal bookEntries.Length 2 "should have 2 book entries"
            let bookTitlesAndIdsByIsdn = addBookToIsbBnRegistry2.GetBooksTitlesAndIdsByIsdn isbn  |> Result.get
            let bookTitlesAndIdsByIsdn1 = 
                {
                    Title = title
                    BookId = bookId1
                }
            let bookTitlesAndIdsByIsdn2 = 
                {
                    Title = title
                    BookId = bookId2
                }
            Expect.equal (bookTitlesAndIdsByIsdn |> List.sort) ( [bookTitlesAndIdsByIsdn1; bookTitlesAndIdsByIsdn2] |> List.sort) "should have 2 book titles and ids"


            
    ]