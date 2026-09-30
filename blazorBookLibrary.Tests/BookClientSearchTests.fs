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
open BookLibrary.Shared
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

            testCaseTask "BookClientService UpdateSbnCodeAsync calls POST api/Books/{id}/sbn with sbn code string" <| fun _ -> task {
                let mutable capturedRequest: HttpRequestMessage option = None
                let handler = new FakeHttpMessageHandler(fun req ->
                    capturedRequest <- Some req
                    let response = new HttpResponseMessage(HttpStatusCode.OK)
                    response.Content <- new StringContent("", System.Text.Encoding.UTF8, "application/json")
                    response
                )
                let client = new HttpClient(handler)
                client.BaseAddress <- Uri("http://localhost/")
                let clientService = BookClientService(client)
                let bookId = BookId.New()
                let sbn = SbnCode.NewValid "CFI0001234" |> Result.get
                let! res = clientService.UpdateSbnCodeAsync(UserContext.Anonymous, sbn, bookId, None)
                Expect.isOk res "UpdateSbnCodeAsync should succeed"
                Expect.isSome capturedRequest "Should have captured request"
                let req = capturedRequest.Value
                Expect.equal req.Method HttpMethod.Post "Method should be POST"
                Expect.equal req.RequestUri.AbsolutePath (sprintf "/api/Books/%s/sbn" (bookId.Value.ToString())) "Path should match"
            }

            testCaseTask "BookClientService UnsetSbnCodeAsync calls DELETE api/Books/{id}/sbn" <| fun _ -> task {
                let mutable capturedRequest: HttpRequestMessage option = None
                let handler = new FakeHttpMessageHandler(fun req ->
                    capturedRequest <- Some req
                    let response = new HttpResponseMessage(HttpStatusCode.OK)
                    response.Content <- new StringContent("", System.Text.Encoding.UTF8, "application/json")
                    response
                )
                let client = new HttpClient(handler)
                client.BaseAddress <- Uri("http://localhost/")
                let clientService = BookClientService(client)
                let bookId = BookId.New()
                let! res = clientService.UnsetSbnCodeAsync(UserContext.Anonymous, bookId, None)
                Expect.isOk res "UnsetSbnCodeAsync should succeed"
                Expect.isSome capturedRequest "Should have captured request"
                let req = capturedRequest.Value
                Expect.equal req.Method HttpMethod.Delete "Method should be DELETE"
                Expect.equal req.RequestUri.AbsolutePath (sprintf "/api/Books/%s/sbn" (bookId.Value.ToString())) "Path should match"
            }

            testCase "SearchCriteria.searchBySbnCode matches books with substring in SbnCode" <| fun _ ->
                let sbn1 = SbnCode.NewValid "CFI0001234" |> Result.get
                let sbnBook = { book1 with SbnCode = Some sbn1 }
                let noSbnBook = { book2 with SbnCode = None }
                
                let criteriaExact = SearchCriteria.searchBySbnCode "CFI0001234"
                let criteriaPartial = SearchCriteria.searchBySbnCode "CFI"
                let criteriaCase = SearchCriteria.searchBySbnCode "cfi000"
                let criteriaMismatch = SearchCriteria.searchBySbnCode "RMS"

                Expect.isTrue (criteriaExact.Invoke sbnBook) "Should match exact SBN"
                Expect.isTrue (criteriaPartial.Invoke sbnBook) "Should match partial SBN"
                Expect.isTrue (criteriaCase.Invoke sbnBook) "Should match case-insensitively"
                Expect.isFalse (criteriaMismatch.Invoke sbnBook) "Should not match different SBN"
                Expect.isFalse (criteriaExact.Invoke noSbnBook) "Should not match book without SBN"

            testCase "SearchCriteria.searchHasSbnCode and searchMissingSbnCode" <| fun _ ->
                let sbn1 = SbnCode.NewValid "CFI0001234" |> Result.get
                let sbnBook = { book1 with SbnCode = Some sbn1 }
                let noSbnBook = { book2 with SbnCode = None }

                Expect.isTrue (SearchCriteria.searchHasSbnCode.Invoke sbnBook) "HasSbn on sbnBook should be true"
                Expect.isFalse (SearchCriteria.searchHasSbnCode.Invoke noSbnBook) "HasSbn on noSbnBook should be false"
                Expect.isFalse (SearchCriteria.searchMissingSbnCode.Invoke sbnBook) "MissingSbn on sbnBook should be false"
                Expect.isTrue (SearchCriteria.searchMissingSbnCode.Invoke noSbnBook) "MissingSbn on noSbnBook should be true"

            testCase "SearchCriteria delegate composition with composeAll, combine, And, Or, Not" <| fun _ ->
                let sbn1 = SbnCode.NewValid "CFI0001234" |> Result.get
                let bookWithSbnAndAvail = { book1 with SbnCode = Some sbn1 }
                let bookWithSbnNotAvail = { book2 with SbnCode = Some sbn1 }
                let bookWithoutSbnAvail = { book3 with SbnCode = None }

                let sbnCriteria = SearchCriteria.searchBySbnCode "CFI"
                let availCriteria = SearchCriteria.searchImmediatelyAvailable
                let composed = SearchCriteria.composeAll [ sbnCriteria; availCriteria ]

                Expect.isTrue (composed.Invoke bookWithSbnAndAvail) "bookWithSbnAndAvail satisfies both"
                Expect.isFalse (composed.Invoke bookWithSbnNotAvail) "bookWithSbnNotAvail fails availability"
                Expect.isFalse (composed.Invoke bookWithoutSbnAvail) "bookWithoutSbnAvail fails SBN"

                let andCriteria = sbnCriteria.And(availCriteria)
                Expect.isTrue (andCriteria.Invoke bookWithSbnAndAvail) ".And should match when both pass"
                Expect.isFalse (andCriteria.Invoke bookWithSbnNotAvail) ".And should fail when one fails"

                let orCriteria = sbnCriteria.Or(availCriteria)
                Expect.isTrue (orCriteria.Invoke bookWithSbnNotAvail) ".Or should match when SBN passes"
                Expect.isTrue (orCriteria.Invoke bookWithoutSbnAvail) ".Or should match when availability passes"

                let notCriteria = sbnCriteria.Not()
                Expect.isFalse (notCriteria.Invoke bookWithSbnAndAvail) ".Not on sbnBook should be false"
                Expect.isTrue (notCriteria.Invoke bookWithoutSbnAvail) ".Not on noSbnBook should be true"

            testCaseTask "GetAllAsync with composed SbnCode and DP criteria filters accurately" <| fun _ -> task {
                let sbn1 = SbnCode.NewValid "CFI0001234" |> Result.get
                let sbn2 = SbnCode.NewValid "RMS0005678" |> Result.get
                let testBook1 = { book1 with SbnCode = Some sbn1 }
                let testBook2 = { book2 with SbnCode = Some sbn2 }
                let testBook3 = { book3 with SbnCode = Some sbn1 }

                let httpClient = createMockClient [ testBook1; testBook2; testBook3 ]
                let clientService = BookClientService(httpClient)

                let criteria = SearchCriteria.composeAll [
                    SearchCriteria.searchBySbnCode "CFI"
                    SearchCriteria.searchByDistributionPoints [ dpId1 ]
                ]

                let! result = clientService.GetAllAsync(UserContext.Anonymous, Some criteria, None)
                match result with
                | Ok books ->
                    Expect.equal books.Length 2 "Should return testBook1 and testBook3"
                    let titles = books |> List.map (fun b -> b.Title.Value)
                    Expect.contains titles "F# in Action" "Contains book1"
                    Expect.contains titles "The Hobbit" "Contains book3"
                | Error err -> failwithf "Failed: %s" err
            }
        ]
