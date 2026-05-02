module ImportExportDataTest

open System
open System.Threading
open Expecto
open TestSetup
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open BookLibrary.Shared.Details

[<Tests>]
let tests =
    // todo: long running tests (or progress bar may inhibit testing, to be investigated)
    ptestList "ImportExportData tests" [
        testCaseTask "Import from ISBN 9780593135211 (Project Hail Mary)" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)
            let isbn = Isbn "9780593135211"
            
            // Import
            let! result = dataExportService.ImportFromIsbns([isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            // Verify Book
            let! booksResult = bookService.SearchByIsbnAsync isbn
            Expect.isOk booksResult "Should be able to search by ISBN"
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 1 "Should have imported exactly 1 book"
            
            let book = List.head books
            Expect.stringContains (book.Title.Value.ToLower()) "hail mary" "Title should contain 'Hail Mary'"
            Expect.isSome book.Description "Description should be present"
            Expect.isSome book.ImageUrl "Book image URL should be present"
            Expect.isFalse (List.isEmpty book.Authors) "Should have at least one author"
            
            // Verify Author
            let! authorResult = authorService.GetAuthorAsync (List.head book.Authors)
            Expect.isOk authorResult "Author should exist"
            let author = authorResult |> Result.get
            Expect.stringContains (author.Name.Value.ToLower()) "andy weir" "Author should be Andy Weir"
            Expect.isSome author.ImageUri "Author image URL should be present"
        }
        
        testCaseTask "Import from ISBN 9791259767349 (Partial data)" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)
            let isbn = Isbn "9791259767349"
            
            // Import
            let! result = dataExportService.ImportFromIsbns([isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful even if author picture lookup fails"
            
            // Verify Book
            let! booksResult = bookService.SearchByIsbnAsync isbn
            Expect.isOk booksResult "Should be able to search by ISBN"
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 1 "Should have imported exactly 1 book"
            
            let book = List.head books
            Expect.isFalse (String.IsNullOrWhiteSpace book.Title.Value) "Title should be present"
            Expect.isNone book.Description "Description should be empty for this ISBN"
            Expect.isSome book.ImageUrl "Book image URL should be present"
            Expect.isFalse (List.isEmpty book.Authors) "Should have at least one author"
            
            // Verify Author
            let! authorResult = authorService.GetAuthorAsync (List.head book.Authors)
            Expect.isOk authorResult "Author should exist"
            let author = authorResult |> Result.get
            Expect.isNone author.ImageUri "Author image URL should be absent for this author"
        }

        testCaseTask "Import from unresolvable ISBN 9999999999999" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let isbn = Isbn "9999999999999"
            
            let! result = dataExportService.ImportFromIsbns([isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should succeed (by skipping unresolvable ISBN)"
            
            let bookService = (getBookService () :> IBookService)
            let! booksResult = bookService.GetAllAsync()
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 0 "No books should have been imported"
        }
        
        testCaseTask "Import from invalid ISBN string '123'" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let isbn = Isbn "123"
            
            let! result = dataExportService.ImportFromIsbns([isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should succeed (by skipping invalid ISBN)"
            
            let bookService = (getBookService () :> IBookService)
            let! booksResult = bookService.GetAllAsync()
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 0 "No books should have been imported"
        }

        testCaseTask "Import from empty ISBN list" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let! result = dataExportService.ImportFromIsbns([], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should succeed for empty list"
        }

        testCaseTask "Massive ISBN import" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let isbnsStr = [
                "9780860122999"
                "9780792322009"
                "9780300177541"
                "9780241304549"
                "9781761060939"
                "9781628387292"
                "9788820112547"
                "9788806185824"
            ]
            let isbns = isbnsStr |> List.map Isbn
            
            // Import
            let! result = dataExportService.ImportFromIsbns(isbns, true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Massive import should be successful"
            let importSummary = result |> Result.get
            Expect.equal importSummary.SuccessCount (List.length isbnsStr) "All ISBNs should have been successfully imported"
            Expect.equal importSummary.FailureCount 0 "No ISBNs should have failed"
            
            // Verify all books are imported
            let! booksResult = bookService.GetAllAsync()
            Expect.isOk booksResult "Should be able to get all books"
            let books = booksResult |> Result.get
            
            Expect.equal (List.length books) (List.length isbnsStr) "All ISBNs should have been imported as books"
            
            // Verify each ISBN exists in the imported collection
            let mutable failedIsbns = []
            for isbnStr in isbnsStr do
                let exists = books |> List.exists (fun b -> b.Isbn.Value = isbnStr)
                if not exists then
                    failedIsbns <- isbnStr :: failedIsbns
            
            Expect.isEmpty failedIsbns (sprintf "The following ISBNs failed to import: %A" (List.rev failedIsbns))
        }

        testCaseTask "Import counts verification" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let validIsbn = Isbn "9780593135211" // Project Hail Mary
            let unresolvableIsbn = Isbn "9999999999999"
            
            let! result = dataExportService.ImportFromIsbns([validIsbn; unresolvableIsbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            let importSummary = result |> Result.get
            Expect.equal importSummary.SuccessCount 1 "Should have 1 successful import"
            Expect.equal importSummary.FailureCount 1 "Should have 1 failed import"
        }

        testCaseTask "Import mix of valid and unresolvable ISBNs" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let validIsbn = Isbn "9780593135211" // Project Hail Mary
            let unresolvableIsbn = Isbn "9999999999999"
            
            let! result = dataExportService.ImportFromIsbns([validIsbn; unresolvableIsbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            
            let bookService = (getBookService () :> IBookService)
            let! booksResult = bookService.GetAllAsync()
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 1 "Only one book should have been imported"
        }

        testCaseTask "Import progress verification" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let isbns = [Isbn "9780593135211"; Isbn "9999999999999"]
            
            let mutable reportedProgress = []
            let progress = 
                { new IProgress<ImportProgress> with
                    member _.Report(p) = reportedProgress <- p :: reportedProgress
                }
            
            let! result = dataExportService.ImportFromIsbns(isbns, true, true, false, false, progress, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            
            Expect.equal (List.length reportedProgress) 2 "Should have reported progress twice"
            let finalProgress = List.head reportedProgress
            Expect.equal finalProgress.Current 2 "Final progress should be 2"
            Expect.equal finalProgress.Total 2 "Total progress should be 2"
        }
    ]
    |> testSequenced
