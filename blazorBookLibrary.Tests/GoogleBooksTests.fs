namespace BookLibrary.Tests

open Expecto
open Microsoft.Extensions.Configuration
open System.Net.Http
open BookLibrary.Services
open BookLibrary.Shared.Services
open System.IO

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
        testList "Google Books Service Tests" [
            testCaseAsync "can lookup book by ISBN" <| async {
                let isbn = "9780132350884" // Clean Code
                let! result = googleService.LookupByIsbnAsync(isbn) |> Async.AwaitTask
                match result with
                | Ok (Some metadata) ->
                    Expect.isNotNull metadata.Title "Title should not be null"
                    Expect.isTrue (metadata.Title.Contains("Code", System.StringComparison.OrdinalIgnoreCase)) "Title should contain Code"
                    printfn "Found book: %s" metadata.Title
                | Ok None ->
                    failwith "Book not found"
                | Error e ->
                    failwith e
            }
            testCaseAsync "can lookup book by Title" <| async {
                let title = "The Lord of the Rings"
                let! result = googleService.LookupByTitleAsync(title) |> Async.AwaitTask
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
            testCaseAsync "can lookup multiple books by Title" <| async {
                let title = "The Lord of the Rings"
                let! result = googleService.LookupMultipleByTitleAsync(title) |> Async.AwaitTask
                match result with
                | Ok list ->
                    Expect.isTrue (list.Length > 0) "Should find at least one book"
                    list |> List.iter (fun m -> printfn "Found: %s" m.Title)
                | Error e ->
                    failwith e
            }
        ]
