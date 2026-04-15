namespace BookLibrary.Tests

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
        ptestList "Google Books Service Tests" [
            testCaseTask "can lookup book by ISBN and populate Description" <| fun _ -> task {
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
            testCaseTask "can lookup book by Title" <| fun _ -> task {
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
            testCaseTask "can lookup multiple books by Title" <| fun _ -> task {
                let title = "The Lord of the Rings"
                let! result = googleService.LookupMultipleByTitleAsync(title)
                match result with
                | Ok list ->
                    Expect.isTrue (list.Length > 0) "Should find at least one book"
                    list |> List.iter (fun m -> printfn "Found: %s" m.Title)
                | Error e ->
                    failwith e
            }
            testCaseTask "can lookup cover image by ISBN (Open Library)" <| fun _ -> task {
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
            testCaseTask "returns error for invalid ISBN in cover lookup" <| fun _ -> task {
                let isbn = InvalidIsbn "123"
                let! result = googleService.LookupCoverImageByIsbnAsync(isbn)
                match result with
                | Error msg -> Expect.stringContains msg "invalid" "Should return error message for invalid ISBN"
                | _ -> failwith "Should have returned an error"
            }

            // service may be down
            ftestCaseTask "can lookup cover image from Google API" <| fun _ -> task {
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
        ]
