namespace BookLibrary.Tests

open Expecto
open System.Net.Http
open BookLibrary.Services
open BookLibrary.Shared.Services

module AuthorsSearchTests =
    let getService () =
        let httpClient = new HttpClient()
        httpClient.DefaultRequestHeaders.Add("User-Agent", "BlazorBookLibrary/1.0")
        AuthorsSearchService(httpClient) :> IAuthorsSearchService

    let authService = getService()

    [<Tests>]
    let tests =
        // test pending as the service may be down
        ptestList "Authors Search Service Tests" [
            testCaseAsync "can lookup author by name" <| async {
                let name = "J. R. R. Tolkien"
                let! result = authService.LookupByNameAsync(name) |> Async.AwaitTask
                match result with
                | Ok metadata ->
                    Expect.isNotNull metadata.Name "Author Name should not be null"
                    Expect.isTrue (metadata.Name.Contains("Tolkien", System.StringComparison.OrdinalIgnoreCase)) "Author name should contain Tolkien"
                    printfn "Found author: %s" metadata.Name
                    printf "isni: %A" metadata.Isni
                | Error e ->
                    failwith e
            }

            testCaseAsync "can lookup author thumbnail by name" <| async {
                let name = "Dante Alighieri"
                let! result = authService.LookupImageUrlByNameAndThumbSizeAsync(name, 200) |> Async.AwaitTask
                match result with
                | Ok url ->
                    Expect.isNotNull url "Thumbnail URL should not be null"
                    Expect.isTrue (url.StartsWith("https://upload.wikimedia.org/")) $"URL should be a Wikimedia upload URL: {url}"
                    printfn "Found thumbnail for %s: %s" name url
                | Error e ->
                    failwith e
            }
        ]
        |> testSequenced

