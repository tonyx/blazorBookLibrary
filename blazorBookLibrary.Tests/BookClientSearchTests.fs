namespace BookLibrary.Tests

open Expecto
open System
open System.Net
open System.Net.Http
open System.Threading
open System.Threading.Tasks
open System.Text.Json
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open blazorBookLibrary.Client.Services

type FakeHttpMessageHandler(handler: HttpRequestMessage -> HttpResponseMessage) =
    inherit HttpMessageHandler()
    override this.SendAsync(request: HttpRequestMessage, cancellationToken: CancellationToken) =
        let response = handler request
        Task.FromResult(response)

module BookClientSearchTests =
    
    let createMockClient (books: Book list) =
        let handler = new FakeHttpMessageHandler(fun req ->
            let json = JsonSerializer.Serialize(books, ServiceClientHelper.JsonOptions)
            let response = new HttpResponseMessage(HttpStatusCode.OK)
            response.Content <- new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            response
        )
        let client = new HttpClient(handler)
        client.BaseAddress <- Uri("http://localhost/")
        client

    let t1 = TenantId.Default
    
    // Construct some dummy IDs
    let dpId1 = DistributionPointId (Guid.NewGuid())
    let dpId2 = DistributionPointId (Guid.NewGuid())
    
    let tagSci = Tag.BookTag "Science"
    let tagHistory = Tag.BookTag "History"

    // Book 1: immediately available, Category.Science, tagSci, DP1
    let book1 = 
        { Book.New t1 (Title.New "F# in Action") [] [] [] None Category.Science [] (Year.New 2021) (Isbn.Isbn "1234567890") None
            with Tags = [tagSci]; DistributionPoint = Some dpId1 }

    // Book 2: ReferenceOnly (NOT immediately available), Category.Science, tagSci, DP2
    let book2 = 
        { Book.NewWithAvailability t1 (Title.New "Real-World Functional Programming") [] [] [] None Category.Science [] [tagSci] (Year.New 2011) (Isbn.Isbn "0987654321") None Availability.ReferenceOnly
            with DistributionPoint = Some dpId2 }

    // Book 3: Immediately available, Category.Other, tagHistory, DP1
    let book3 = 
        { Book.New t1 (Title.New "The Hobbit") [] [] [] None Category.Other [] (Year.New 1937) (Isbn.NewEmpty()) None
            with Tags = [tagHistory]; DistributionPoint = Some dpId1 }

    let mockBooks = [ book1; book2; book3 ]

    [<Tests>]
    let tests =
        testList "Book Client Service Search Criteria Tests" [
            
            testCaseTask "GetAllAsync with immediatelyAvailable filter returns only immediately available books" <| fun _ -> task {
                let httpClient = createMockClient mockBooks
                let clientService = BookClientService(httpClient)
                
                // Immediately available filter
                let criteria = BookSearchCriteria(fun b -> b.ImmediatelyAvailable)
                
                let! result = clientService.GetAllAsync(UserContext.Anonymous, Some criteria, None)
                
                match result with
                | Ok books ->
                    // book1 (Circulating, available) and book3 (Circulating, available) should be returned
                    // book2 (ReferenceOnly) is NOT immediately available
                    let bookTitles = books |> Seq.map (fun b -> b.Title.Value) |> Seq.toList
                    Expect.equal books.Length 2 "Should have filtered out the ReferenceOnly book"
                    Expect.contains bookTitles "F# in Action" "Should contain book1"
                    Expect.contains bookTitles "The Hobbit" "Should contain book3"
                    Expect.isFalse (bookTitles |> List.contains "Real-World Functional Programming") "Should not contain book2"
                | Error err ->
                    failwithf "GetAllAsync failed with error: %s" err
            }

            testCaseTask "SearchByTitleAsync with tag filter returns only matching tagged books" <| fun _ -> task {
                let httpClient = createMockClient mockBooks
                let clientService = BookClientService(httpClient)
                
                // Tag "Science" filter
                let criteria = BookSearchCriteria(fun b -> b.Tags |> List.contains tagSci)
                
                let! result = clientService.SearchByTitleAsync(UserContext.Anonymous, Title.New "F#", Some criteria, None)
                
                match result with
                | Ok books ->
                    // book1 (tagSci) and book2 (tagSci) should be returned
                    // book3 (tagHistory) is filtered out
                    let bookTitles = books |> Seq.map (fun b -> b.Title.Value) |> Seq.toList
                    Expect.equal books.Length 2 "Should have filtered out book3 with wrong tag"
                    Expect.contains bookTitles "F# in Action" "Should contain book1"
                    Expect.contains bookTitles "Real-World Functional Programming" "Should contain book2"
                | Error err ->
                    failwithf "SearchByTitleAsync failed with error: %s" err
            }

            testCaseTask "SearchByIsbnAsync with distributionPoint filter returns only books from that distribution point" <| fun _ -> task {
                let httpClient = createMockClient mockBooks
                let clientService = BookClientService(httpClient)
                
                // DistributionPoint 1 filter
                let criteria = BookSearchCriteria(fun b -> b.DistributionPoint = Some dpId1)
                
                let! result = clientService.SearchByIsbnAsync(UserContext.Anonymous, Isbn.Isbn "1234567890", Some criteria, None)
                
                match result with
                | Ok books ->
                    // book1 (dpId1) and book3 (dpId1) should be returned
                    // book2 (dpId2) is filtered out
                    let bookTitles = books |> Seq.map (fun b -> b.Title.Value) |> Seq.toList
                    Expect.equal books.Length 2 "Should have filtered out book2 with different DP"
                    Expect.contains bookTitles "F# in Action" "Should contain book1"
                    Expect.contains bookTitles "The Hobbit" "Should contain book3"
                | Error err ->
                    failwithf "SearchByIsbnAsync failed with error: %s" err
            }
        ]
