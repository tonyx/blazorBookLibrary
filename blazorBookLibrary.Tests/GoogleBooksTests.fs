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
open TestSetup

module GoogleBooksTests =
    let getService () =
        let config = ConfigurationBuilder().AddJsonFile("appSettings.json").Build()

        let httpClient = new HttpClient()

        GoogleBooksService(httpClient, config, tenantViewerAsync, getUserTenantResolverService ())
        :> IBooksMetadataSearchService

    let googleService = getService ()

    [<Tests>]
    let tests =
        // test pending as Google Books API may be down
        testList
            "Google Books Service Tests"
            [ ptestCaseTask "can lookup book by ISBN and populate Description"
              <| fun _ ->
                  task {
                      let isbn = "9780132350884" // Clean Code
                      let! result = googleService.LookupByIsbnAsync(adminContext, isbn)

                      match result with
                      | Ok(Some metadata) ->
                          Expect.isNotNull metadata.Title "Title should not be null"

                          Expect.isTrue
                              (metadata.Title.Contains("Code", System.StringComparison.OrdinalIgnoreCase))
                              "Title should contain Code"

                          Expect.isSome metadata.Description "Description should be populated"
                          Expect.isTrue (metadata.Description.Value.Length > 0) "Description should have content"
                          printfn "Found book: %s" metadata.Title
                      | Ok None -> failwith "Book not found"
                      | Error e -> failwith e
                  }
              ptestCaseTask "can lookup book by Title"
              <| fun _ ->
                  task {
                      let title = "The Lord of the Rings"
                      let! result = googleService.LookupByTitleAsync(adminContext, title)

                      match result with
                      | Ok(Some metadata) ->
                          Expect.isNotNull metadata.Title "Title should not be null"

                          Expect.isTrue
                              (metadata.Title.Contains("Rings", System.StringComparison.OrdinalIgnoreCase))
                              "Title should contain Rings"

                          printfn "Found book by title: %s" metadata.Title
                      | Ok None -> failwith "Book not found by title"
                      | Error e -> failwith e
                  }
              ptestCaseTask "can lookup multiple books by Title"
              <| fun _ ->
                  task {
                      let title = "The Lord of the Rings"
                      let! result = googleService.LookupMultipleByTitleAsync(adminContext, title)

                      match result with
                      | Ok list ->
                          Expect.isTrue (list.Length > 0) "Should find at least one book"
                          list |> List.iter (fun m -> printfn "Found: %s" m.Title)
                      | Error e -> failwith e
                  }
              testCaseTask "can lookup cover image by ISBN (Open Library)"
              <| fun _ ->
                  task {
                      let isbnStr = "9788804668237"
                      let isbn = Isbn isbnStr

                      // Test default size (Medium)
                      let! resultM = googleService.LookupCoverImageByIsbnAsync(adminContext, isbn)

                      match resultM with
                      | Ok(Some url) ->
                          // The URL now should be the final redirected URL, usually on archive.org
                          // and not the search URL 'https://covers.openlibrary.org/b/isbn/9788804668237-M.jpg'
                          Expect.isFalse
                              (url.Contains("covers.openlibrary.org"))
                              "URL should be a direct link, not the search endpoint"

                          Expect.isTrue
                              (url.Contains("archive.org"))
                              "URL should point to archive.org (the typical storage for Open Library covers)"

                          printfn "Found actual cover URL: %s" url
                      | Ok None -> failwith "No cover found for a known book"
                      | Error e -> failwith e

                      // Test Small size
                      let! resultS = googleService.LookupCoverImageByIsbnAsync(adminContext, isbn, ThumbRoughSize.Small)

                      match resultS with
                      | Ok(Some url) ->
                          Expect.isTrue (url.Contains("archive.org")) "Small cover should also point to archive.org"
                      | _ -> failwith "Failed to lookup Small cover"

                      // Test Large size
                      let! resultL = googleService.LookupCoverImageByIsbnAsync(adminContext, isbn, ThumbRoughSize.Large)

                      match resultL with
                      | Ok(Some url) ->
                          Expect.isTrue (url.Contains("archive.org")) "Large cover should also point to archive.org"
                      | _ -> failwith "Failed to lookup Large cover"
                  }
              testCaseTask "returns error for invalid ISBN in cover lookup"
              <| fun _ ->
                  task {
                      let isbn = InvalidIsbn "123"
                      let! result = googleService.LookupCoverImageByIsbnAsync(adminContext, isbn)

                      match result with
                      | Error msg -> Expect.stringContains msg "invalid" "Should return error message for invalid ISBN"
                      | _ -> failwith "Should have returned an error"
                  }

              testCaseTask "can lookup cover image for 'La coscienza di Zeno' by title and author"
              <| fun _ ->
                  task {
                      let title = "La coscienza di Zeno"
                      let author = Some "Italo Svevo"
                      let! result = googleService.LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(adminContext, title, ?author = author)

                      match result with
                      | Ok(Some url) ->
                          Expect.isTrue (url.StartsWith("http")) "URL should start with http or https"
                          Expect.isFalse (url.Contains("blank", StringComparison.OrdinalIgnoreCase)) "URL should not be blank"
                          printfn "Found cover URL for 'La coscienza di Zeno': %s" url
                      | Ok None -> failwith "Should have found a cover for 'La coscienza di Zeno'"
                      | Error e -> failwith e
                  }

              testCaseTask "can lookup cover image for 'La coscienza di Zeno' by title alone"
              <| fun _ ->
                  task {
                      let title = "La coscienza di Zeno"
                      let! result = googleService.LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(adminContext, title)

                      match result with
                      | Ok(Some url) ->
                          Expect.isTrue (url.StartsWith("http")) "URL should start with http or https"
                          Expect.isFalse (url.Contains("blank", StringComparison.OrdinalIgnoreCase)) "URL should not be blank"
                          printfn "Found cover URL for 'La coscienza di Zeno' by title alone: %s" url
                      | Ok None -> failwith "Should have found a cover for 'La coscienza di Zeno'"
                      | Error e -> failwith e
                  }

              testCaseTask "LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync can find cover for valid book"
              <| fun _ ->
                  task {
                      let isbn = Isbn "9788804668237"
                      let! result = googleService.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(adminContext, isbn)
                      match result with
                      | Ok(Some url) ->
                          Expect.isTrue (url.StartsWith("http")) "Should return valid URL"
                          printfn "Found cover via OpenApi/Google combo: %s" url
                      | Ok None -> failwith "Expected to find a cover"
                      | Error e -> failwith e
                  }

              testCaseTask "does not return white or fake image for fictitious book title"
              <| fun _ ->
                  task {
                      let title = "XyZ123987NonExistentBookCompletelyFake_QwErTy"
                      let! result = googleService.LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(adminContext, title)

                      match result with
                      | Ok None -> ()
                      | Ok(Some url) -> failwithf "Expected no cover for fictitious book, but got: %s" url
                      | Error _ -> ()
                  }

              // service may be down
              ptestCaseTask "can lookup cover image from Google API"
              <| fun _ ->
                  task {
                      let isbnStr = "9780132350884" // Clean Code
                      let isbn = Isbn isbnStr
                      let! result = googleService.LookupGoogleApiCoverImageByIsbnAsync(adminContext, isbn)

                      match result with
                      | Ok(Some url) ->
                          Expect.isTrue (url.StartsWith("http")) "Should return a valid URL"
                          Expect.stringContains url "books.google.com" "URL should point to Google Books"
                          printfn "Found Google cover URL: %s" url
                      | Ok None -> failwith "Should have found a cover for Clean Code"
                      | Error e -> failwith e
                  }

              ptestCaseTask "can identify book from cover image (Gemini)"
              <| fun _ ->
                  task {
                      let embeddingService = TestSetup.getTextEmbeddingService ()

                      let imgPath =
                          Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testImgData", "sampleCover.jpg")

                      if not (File.Exists imgPath) then
                          // Fallback for some test runners that don't copy properly or run from different CWD
                          let fallbackPath = Path.Combine("..", "..", "..", "testImgData", "sampleCover.jpg")

                          if File.Exists fallbackPath then
                              let base64Image = System.Convert.ToBase64String(File.ReadAllBytes fallbackPath)

                              let! (result: Result<PartialBookDataMatch, string>) =
                                  embeddingService.GetPartialBookMatchByCoverImage(
                                      adminContext,
                                      base64Image,
                                      "image/jpeg"
                                  )

                              match result with
                              | Ok(matchData: PartialBookDataMatch) ->
                                  printfn "Identified Book: %A" matchData

                                  Expect.isTrue
                                      (matchData.IsValidTitle || matchData.IsValidIsbn)
                                      "Should identify metadata"
                              | Error e -> failwith e
                          else
                              failwithf "Test image not found at %s or %s" imgPath fallbackPath
                      else
                          let base64Image = System.Convert.ToBase64String(File.ReadAllBytes imgPath)

                          let! (result: Result<PartialBookDataMatch, string>) =
                              embeddingService.GetPartialBookMatchByCoverImage(adminContext, base64Image, "image/jpeg")

                          match result with
                          | Ok(matchData: PartialBookDataMatch) ->
                              printfn "Identified Book: %A" matchData

                              Expect.isTrue
                                  (matchData.IsValidTitle || matchData.IsValidIsbn)
                                  "Should identify either title or ISBN from cover"
                          | Error e -> failwith e
                  }
              testCase "can parse Gemini metadata response with singular author array (reported user bug)"
              <| fun _ ->
                  let json = """[ { "title": "E verrà un altro inverno", "author": [ "Massimo Carlotto" ], "categories": [ "Fiction", "Crime", "Noir" ], "year": 2021, "isbn": "9788817154017", "description": "Un noir teso e spietato ambientato nella valle della ricca provincia veneta, dove una rapina finita male scoperchia i segreti, la cupidigia e la decadenza morale di una comunità apparentemente irreprensibile." } ]"""
                  let cleaned = GoogleGeminiHelpers.cleanJsonCodeBlock json
                  let list = GoogleGeminiHelpers.parseBooksMetadataList cleaned
                  Expect.equal list.Length 1 "Should parse exactly 1 book"
                  let book = list.[0]
                  Expect.equal book.Title "E verrà un altro inverno" "Title should match"
                  Expect.equal (book.Authors |> Seq.toList) [ "Massimo Carlotto" ] "Authors should contain Massimo Carlotto"
                  Expect.equal (book.Categories |> Seq.toList) [ "Fiction"; "Crime"; "Noir" ] "Categories should match"
                  Expect.equal book.Year (Some 2021) "Year should be 2021"
                  Expect.equal book.Isbn (Some "9788817154017") "ISBN should match"
                  Expect.isSome book.Description "Description should be populated"

              testCase "can parse Gemini metadata response with singular author string and code fences"
              <| fun _ ->
                  let json = """```json
[ { "title": "Test Book", "author": "John Doe", "categories": "Fiction, Sci-Fi", "year": "2020", "isbn": "978-0132350884" } ]
```"""
                  let cleaned = GoogleGeminiHelpers.cleanJsonCodeBlock json
                  let list = GoogleGeminiHelpers.parseBooksMetadataList cleaned
                  Expect.equal list.Length 1 "Should parse exactly 1 book"
                  let book = list.[0]
                  Expect.equal book.Title "Test Book" "Title should match"
                  Expect.equal (book.Authors |> Seq.toList) [ "John Doe" ] "Authors should match"
                  Expect.equal (book.Categories |> Seq.toList) [ "Fiction"; "Sci-Fi" ] "Categories should match"
                  Expect.equal book.Year (Some 2020) "Year should be 2020"

              testCase "can parse single book metadata response with missing optional fields"
              <| fun _ ->
                  let json = """{ "title": "Minimal Book", "writers": ["Jane Smith"] }"""
                  let cleaned = GoogleGeminiHelpers.cleanJsonCodeBlock json
                  let bookOpt = GoogleGeminiHelpers.parseSingleBookMetadata cleaned
                  Expect.isSome bookOpt "Should parse book"
                  let book = bookOpt.Value
                  Expect.equal book.Title "Minimal Book" "Title should match"
                  Expect.equal (book.Authors |> Seq.toList) [ "Jane Smith" ] "Authors should match"
                  Expect.equal book.Categories.Count 0 "Categories should be empty"
                  Expect.isNone book.Year "Year should be none"
                  Expect.isNone book.Isbn "Isbn should be none"
                  Expect.isNone book.Description "Description should be none" ]
