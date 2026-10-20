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
let exportTests =
    testList "Export tests" [
        testCaseTask "ExportAllBooksAsync as JSON exports author names and formatted JSON" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Isaac Asimov"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "Foundation"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1951) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, Json)
            Expect.isOk exportResult "Export should succeed"

            let json = exportResult |> Result.get
            // Verify JSON is formatted (contains newlines and indentation)
            Expect.isTrue (json.Contains("\n")) "JSON should be formatted with newlines"
            Expect.isTrue (json.Contains("  \"Authors\":")) "JSON should be indented"

            // Verify author name is present
            Expect.isTrue (json.Contains(authorName)) "JSON should contain author name"

            // Parse JSON and check structure
            use doc = System.Text.Json.JsonDocument.Parse(json)
            let root = doc.RootElement
            Expect.equal root.ValueKind System.Text.Json.JsonValueKind.Array "Root should be array"
            let books = root.EnumerateArray() |> Seq.toList
            Expect.isFalse (List.isEmpty books) "Books array should not be empty"
            let matchingBook = books |> List.find (fun b -> b.GetProperty("Title").GetString() = bookTitle)
            let authors = matchingBook.GetProperty("Authors").EnumerateArray() |> Seq.map (fun a -> a.GetString()) |> Seq.toList
            Expect.contains authors authorName "Authors list should contain author name"
        }

        testCaseTask "ExportAllBooksAsync as BibTeX exports formatted .bib entries" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Arthur C. Clarke"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "2001: A Space Odyssey"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1968) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, BibTex)
            Expect.isOk exportResult "BibTeX Export should succeed"

            let bib = exportResult |> Result.get
            Expect.isTrue (bib.Contains("@book{")) "BibTeX should contain @book entry"
            Expect.isTrue (bib.Contains("title = {")) "BibTeX should contain title"
            Expect.isTrue (bib.Contains(authorName)) "BibTeX should contain author name"
            Expect.isTrue (bib.Contains("1968")) "BibTeX should contain year"
        }

        testCaseTask "ExportAllBooksAsync as SchemaOrg exports valid JSON-LD graph" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Philip K. Dick"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "Do Androids Dream of Electric Sheep?"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1968) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, SchemaOrg)
            Expect.isOk exportResult "Schema.org Export should succeed"

            let jsonld = exportResult |> Result.get
            use doc = System.Text.Json.JsonDocument.Parse(jsonld)
            let root = doc.RootElement
            Expect.equal (root.GetProperty("@context").GetString()) "https://schema.org" "Context should be schema.org"
            let graph = root.GetProperty("@graph").EnumerateArray() |> Seq.toList
            Expect.isFalse (List.isEmpty graph) "@graph should not be empty"
            let matchingBook = graph |> List.find (fun b -> b.GetProperty("name").GetString() = bookTitle)
            Expect.equal (matchingBook.GetProperty("@type").GetString()) "Book" "@type should be Book"
            let authors = matchingBook.GetProperty("author").EnumerateArray() |> Seq.map (fun a -> a.GetProperty("name").GetString()) |> Seq.toList
            Expect.contains authors authorName "Authors list should contain author name"
        }

        testCaseTask "ExportAllBooksAsync as DublinCore exports valid Dublin Core XML" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Isaac Asimov"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "Foundation"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1951) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, DublinCore)
            Expect.isOk exportResult "Dublin Core Export should succeed"

            let xml = exportResult |> Result.get
            Expect.isTrue (xml.Contains("<records xmlns:dc=\"http://purl.org/dc/elements/1.1/\">")) "XML should declare Dublin Core namespace"
            Expect.isTrue (xml.Contains(sprintf "<dc:title>%s</dc:title>" bookTitle)) "XML should contain book title"
            Expect.isTrue (xml.Contains(sprintf "<dc:creator>%s</dc:creator>" authorName)) "XML should contain author name"
            Expect.isTrue (xml.Contains("<dc:date>1951</dc:date>")) "XML should contain year"
            Expect.isTrue (xml.Contains("<dc:type>Text</dc:type>")) "XML should contain type Text"
        }

        testCaseTask "ExportAllBooksAsync as MarcXml exports valid MARCXML collection" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Isaac Asimov"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "Foundation"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1951) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, MarcXml)
            Expect.isOk exportResult "MARCXML Export should succeed"

            let xml = exportResult |> Result.get
            Expect.isTrue (xml.Contains("<collection xmlns=\"http://www.loc.gov/MARC21/slim\">")) "XML should declare MARCXML collection namespace"
            Expect.isTrue (xml.Contains("<leader>00000nam a2200000 a 4500</leader>")) "XML should contain valid leader"
            Expect.isTrue (xml.Contains(sprintf "<subfield code=\"a\">%s</subfield>" bookTitle)) "XML should contain book title in 245"
            Expect.isTrue (xml.Contains(sprintf "<subfield code=\"a\">%s</subfield>" authorName)) "XML should contain author name in 100"
            Expect.isTrue (xml.Contains("<subfield code=\"c\">1951</subfield>")) "XML should contain publication year in 264"
        }

        testCaseTask "ExportAllBooksAsync as Mods exports valid MODS collection" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Isaac Asimov"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "Foundation"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1951) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, Mods)
            Expect.isOk exportResult "MODS Export should succeed"

            let xml = exportResult |> Result.get
            Expect.isTrue (xml.Contains("<modsCollection xmlns=\"http://www.loc.gov/mods/v3\">")) "XML should declare MODS collection namespace"
            Expect.isTrue (xml.Contains(sprintf "<title>%s</title>" bookTitle)) "XML should contain book title"
            Expect.isTrue (xml.Contains(sprintf "<namePart>%s</namePart>" authorName)) "XML should contain author name"
            Expect.isTrue (xml.Contains("<dateIssued keyDate=\"yes\">1951</dateIssued>")) "XML should contain publication year"
            Expect.isTrue (xml.Contains("<typeOfResource>text</typeOfResource>")) "XML should contain type of resource"
        }

        testCaseTask "ExportAllBooksAsync as Onix exports valid ONIX 3.0 message" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)

            let authorName = "Isaac Asimov"
            let author = Author.NewWithoutIsni TenantId.Default (Name.New authorName)
            let! addAuthor = authorService.AddAuthorAsync(adminContext, author)
            Expect.isOk addAuthor "Add author should succeed"

            let bookTitle = "Foundation"
            let book = Book.New TenantId.Default (Title.New bookTitle) [author.AuthorId] [] [] None Category.ScienceFiction [] (Year.New 1951) (Isbn.NewEmpty()) None
            let! addBook = bookService.AddBookAsync(adminContext, book, CancellationToken.None)
            Expect.isOk addBook "Add book should succeed"

            let! exportResult = dataExportService.ExportAllBooksAsync(adminContext, Onix)
            Expect.isOk exportResult "ONIX Export should succeed"

            let xml = exportResult |> Result.get
            Expect.isTrue (xml.Contains("<ONIXMessage release=\"3.0\" xmlns=\"http://ns.editeur.org/onix/3.0/reference\">")) "XML should declare ONIX 3.0 namespace"
            Expect.isTrue (xml.Contains("<NotificationType>03</NotificationType>")) "XML should contain NotificationType 03"
            Expect.isTrue (xml.Contains(sprintf "<TitleText>%s</TitleText>" bookTitle)) "XML should contain book title"
            Expect.isTrue (xml.Contains(sprintf "<PersonName>%s</PersonName>" authorName)) "XML should contain author name"
            Expect.isTrue (xml.Contains("<Date dateformat=\"05\">1951</Date>")) "XML should contain publication year"
        }
    ]

[<Tests>]
let tests =
    // test suspended as they can be slow and the external services may be unavailable or unpredictable
     
    ptestList "ImportExportData tests" [
        // last verification 20260513 was ok
        testCaseTask "Import from ISBN 9780593135211 (Project Hail Mary)" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)
            let isbn = Isbn "9780593135211"
            
            // // Import
            let! result = dataExportService.ImportFromIsbns(adminContext, [isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            // Verify Book
            let! booksResult = bookService.SearchByIsbnAsync(adminContext, isbn)
            Expect.isOk booksResult "Should be able to search by ISBN"
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 1 "Should have imported exactly 1 book"
            
            let book = List.head books
            Expect.stringContains (book.Title.Value.ToLower()) "hail mary" "Title should contain 'Hail Mary'"
            Expect.isSome book.Description "Description should be present"
            Expect.isSome book.ImageUrl "Book image URL should be present"
            Expect.isFalse (List.isEmpty book.Authors) "Should have at least one author"
            
            // Verify Author
            let! authorResult = authorService.GetAuthorAsync(adminContext, List.head book.Authors)
            Expect.isOk authorResult "Author should exist"
            let author = authorResult |> Result.get
            Expect.stringContains (author.Name.Value.ToLower()) "andy weir" "Author should be Andy Weir"
            Expect.isSome author.ImageUri "Author image URL should be present"
        }
        
        // last verification 20260513 was ok
        testCaseTask "Import from ISBN 9791259767349 (Partial data)" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let bookService = (getBookService () :> IBookService)
            let authorService = (getAuthorService () :> IAuthorService)
            let isbn = Isbn "9791259767349"
            
            // Import
            let! result = dataExportService.ImportFromIsbns(adminContext, [isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful even if author picture lookup fails"
            
            // Verify Book
            let! booksResult = bookService.SearchByIsbnAsync(adminContext, isbn)
            Expect.isOk booksResult "Should be able to search by ISBN"
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 1 "Should have imported exactly 1 book"
            
            let book = List.head books
            Expect.isFalse (String.IsNullOrWhiteSpace book.Title.Value) "Title should be present"
            Expect.isNone book.Description "Description should be empty for this ISBN"
            Expect.isSome book.ImageUrl "Book image URL should be present"
            Expect.isFalse (List.isEmpty book.Authors) "Should have at least one author"
            
            // Verify Author
            let! authorResult = authorService.GetAuthorAsync(adminContext, List.head book.Authors)
            Expect.isOk authorResult "Author should exist"
            let author = authorResult |> Result.get
            Expect.isNone author.ImageUri "Author image URL should be absent for this author"
        }

        // last verification 20260513 was ok
        testCaseTask "Import from unresolvable ISBN 9999999999999" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let isbn = Isbn "9999999999999"
            
            let! result = dataExportService.ImportFromIsbns(adminContext, [isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should succeed (by skipping unresolvable ISBN)"
            
            let bookService = (getBookService () :> IBookService)
            let! booksResult = bookService.GetAllAsync(adminContext)
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 0 "No books should have been imported"
        }
        
        testCaseTask "Import from invalid ISBN string '123'" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let isbn = Isbn "123"
            
            let! result = dataExportService.ImportFromIsbns(adminContext, [isbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should succeed (by skipping invalid ISBN)"
            
            let bookService = (getBookService () :> IBookService)
            let! booksResult = bookService.GetAllAsync(adminContext)
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 0 "No books should have been imported"
        }

        // last verification 20260513 was ok
        testCaseTask "Import from empty ISBN list" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let! result = dataExportService.ImportFromIsbns(adminContext, [], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should succeed for empty list"
        }

        // last verification 20260513 was ok
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
            let! result = dataExportService.ImportFromIsbns(adminContext, isbns, true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Massive import should be successful"
            let importSummary = result |> Result.get
            Expect.equal importSummary.SuccessCount (List.length isbnsStr) "All ISBNs should have been successfully imported"
            Expect.equal importSummary.FailureCount 0 "No ISBNs should have failed"
            
            // Verify all books are imported
            let! booksResult = bookService.GetAllAsync(adminContext)
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

        // last verification 20260513 was ok
        testCaseTask "Import counts verification" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let validIsbn = Isbn "9780593135211" // Project Hail Mary
            let unresolvableIsbn = Isbn "9999999999999"
            
            let! result = dataExportService.ImportFromIsbns(adminContext, [validIsbn; unresolvableIsbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            let importSummary = result |> Result.get
            Expect.equal importSummary.SuccessCount 1 "Should have 1 successful import"
            Expect.equal importSummary.FailureCount 1 "Should have 1 failed import"
        }

        // last verification 20260513 was ok
        testCaseTask "Import mix of valid and unresolvable ISBNs" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let validIsbn = Isbn "9780593135211" // Project Hail Mary
            let unresolvableIsbn = Isbn "9999999999999"
            
            let! result = dataExportService.ImportFromIsbns(adminContext, [validIsbn; unresolvableIsbn], true, true, false, false, null, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            
            let bookService = (getBookService () :> IBookService)
            let! booksResult = bookService.GetAllAsync(adminContext)
            let books = booksResult |> Result.get
            Expect.equal (List.length books) 1 "Only one book should have been imported"
        }

        // last verification 20260513 was ok
        testCaseTask "Import progress verification" <| fun _ -> task {
            setUp ()
            let dataExportService = (getDataExportService () :> IDataExportService)
            let isbns = [Isbn "9780593135211"; Isbn "9999999999999"]
            
            let mutable reportedProgress = []
            let progress = 
                { new IProgress<ImportProgress> with
                    member _.Report(p) = reportedProgress <- p :: reportedProgress
                }
            
            let! result = dataExportService.ImportFromIsbns(adminContext, isbns, true, true, false, false, progress, CancellationToken.None)
            Expect.isOk result "Import should be successful"
            
            Expect.equal (List.length reportedProgress) 3 "Should have reported progress twice"
            let finalProgress = List.head reportedProgress
            Expect.equal finalProgress.Current 1 "Final progress should be 1"
            Expect.equal finalProgress.Total 2 "Total progress should be 2"
        }
    ]
    |> testSequenced
