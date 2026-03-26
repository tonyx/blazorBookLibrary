namespace BookLibrary.Tests

open Expecto
open System.Net.Http
open BookLibrary.Services
open BookLibrary.Shared.Services

module AuthorsSearchTests =
    let getService () =
        let httpClient = new HttpClient()
        AuthorsSearchService(httpClient) :> IAuthorsSearchService

    let authService = getService()

    [<Tests>]
    let tests =
        ftestList "Authors Search Service Tests" [
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
        ]
