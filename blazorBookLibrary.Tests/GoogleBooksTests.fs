namespace BookLibrary.Tests

open System
open Expecto
open Microsoft.Extensions.Configuration
open System.Net.Http
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System.IO
open System.Threading

module GoogleBooksTests =
    let getService () =
        let config = 
            ConfigurationBuilder()
                .AddJsonFile("appSettings.json")
                .Build()

        let httpClient = new HttpClient()
        GoogleBooksService(httpClient, config) :> IGoogleBooksService

    let googleService = getService()

    [<Tests>]
    let tests =
        // test pending as Google Books API may be down
        testList "Google Books Service Tests" [
            ptestCaseTask "can lookup book by ISBN and populate Description" <| fun _ -> task {
                let isbn = "9780132350884" // Clean Code
                let! result = googleService.LookupByIsbnAsync(isbn)
                match result with
                | Ok (Some metadata) ->
                    Expect.isNotNull metadata.Title "Title should not be null"
                    Expect.isTrue (metadata.Title.Contains("Code", System.StringComparison.OrdinalIgnoreCase)) "Title should contain Code"
                    Expect.isSome metadata.Description "Description should be populated"
                    Expect.isTrue (metadata.Description.Value.Length > 0) "Description should have content"
                    printfn "Found book: %s" metadata.Title
                | Ok None ->
                    failwith "Book not found"
                | Error e ->
                    failwith e
            }
            ptestCaseTask "can lookup book by Title" <| fun _ -> task {
                let title = "The Lord of the Rings"
                let! result = googleService.LookupByTitleAsync(title)
                match result with
                | Ok (Some metadata) ->
                    Expect.isNotNull metadata.Title "Title should not be null"
                    Expect.isTrue (metadata.Title.Contains("Rings", System.StringComparison.OrdinalIgnoreCase)) "Title should contain Rings"
                    printfn "Found book by title: %s" metadata.Title
                | Ok None ->
                    failwith "Book not found by title"
                | Error e ->
                    failwith e
            }
            ptestCaseTask "can lookup multiple books by Title" <| fun _ -> task {
                let title = "The Lord of the Rings"
                let! result = googleService.LookupMultipleByTitleAsync(title)
                match result with
                | Ok list ->
                    Expect.isTrue (list.Length > 0) "Should find at least one book"
                    list |> List.iter (fun m -> printfn "Found: %s" m.Title)
                | Error e ->
                    failwith e
            }
            ptestCaseTask "can lookup cover image by ISBN (Open Library)" <| fun _ -> task {
                let isbnStr = "9788804668237"
                let isbn = Isbn isbnStr
                
                // Test default size (Medium)
                let! resultM = googleService.LookupCoverImageByIsbnAsync(isbn)
                match resultM with
                | Ok (Some url) ->
                    // The URL now should be the final redirected URL, usually on archive.org
                    // and not the search URL 'https://covers.openlibrary.org/b/isbn/9788804668237-M.jpg'
                    Expect.isFalse (url.Contains("covers.openlibrary.org")) "URL should be a direct link, not the search endpoint"
                    Expect.isTrue (url.Contains("archive.org")) "URL should point to archive.org (the typical storage for Open Library covers)"
                    printfn "Found actual cover URL: %s" url
                | Ok None -> 
                    failwith "No cover found for a known book"
                | Error e -> 
                    failwith e

                // Test Small size
                let! resultS = googleService.LookupCoverImageByIsbnAsync(isbn, ThumbRoughSize.Small)
                match resultS with
                | Ok (Some url) ->
                    Expect.isTrue (url.Contains("archive.org")) "Small cover should also point to archive.org"
                | _ -> failwith "Failed to lookup Small cover"

                // Test Large size
                let! resultL = googleService.LookupCoverImageByIsbnAsync(isbn, ThumbRoughSize.Large)
                match resultL with
                | Ok (Some url) ->
                    Expect.isTrue (url.Contains("archive.org")) "Large cover should also point to archive.org"
                | _ -> failwith "Failed to lookup Large cover"
            }
            ptestCaseTask "returns error for invalid ISBN in cover lookup" <| fun _ -> task {
                let isbn = InvalidIsbn "123"
                let! result = googleService.LookupCoverImageByIsbnAsync(isbn)
                match result with
                | Error msg -> Expect.stringContains msg "invalid" "Should return error message for invalid ISBN"
                | _ -> failwith "Should have returned an error"
            }

            // service may be down
            ptestCaseTask "can lookup cover image from Google API" <| fun _ -> task {
                let isbnStr = "9780132350884" // Clean Code
                let isbn = Isbn isbnStr
                let! result = googleService.LookupGoogleApiCoverImageByIsbnAsync(isbn)
                match result with
                | Ok (Some url) ->
                    Expect.isTrue (url.StartsWith("http")) "Should return a valid URL"
                    Expect.stringContains url "books.google.com" "URL should point to Google Books"
                    printfn "Found Google cover URL: %s" url
                | Ok None ->
                    failwith "Should have found a cover for Clean Code"
                | Error e ->
                    failwith e
            }

            ptestCaseTask "can identify book from cover image (Gemini)" <| fun _ -> task {
                let embeddingService = TestSetup.getTextEmbeddingService()
                let imgPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testImgData", "sampleCover.jpg")
                
                if not (File.Exists imgPath) then
                    // Fallback for some test runners that don't copy properly or run from different CWD
                    let fallbackPath = Path.Combine("..", "..", "..", "testImgData", "sampleCover.jpg")
                    if File.Exists fallbackPath then
                        let base64Image = System.Convert.ToBase64String(File.ReadAllBytes fallbackPath)
                        let! result = embeddingService.GetPartialBookMatchByCoverImage(base64Image, "image/jpeg")
                        match result with
                        | Ok matchData -> 
                            printfn "Identified Book: %A" matchData
                            Expect.isTrue (matchData.IsValidTitle || matchData.IsValidIsbn) "Should identify metadata"
                        | Error e -> failwith e
                    else
                        failwithf "Test image not found at %s or %s" imgPath fallbackPath
                else
                    let base64Image = System.Convert.ToBase64String(File.ReadAllBytes imgPath)
                    let! result = embeddingService.GetPartialBookMatchByCoverImage(base64Image, "image/jpeg")
                    match result with
                    | Ok matchData ->
                        printfn "Identified Book: %A" matchData
                        Expect.isTrue (matchData.IsValidTitle || matchData.IsValidIsbn) "Should identify either title or ISBN from cover"
                    | Error e ->
                        failwith e
            }
        ]
