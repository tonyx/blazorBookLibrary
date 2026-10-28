namespace BookLibrary.Services

open System
open System.Net.Http
open System.Text
open System.Text.Json
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open Microsoft.Extensions.Configuration
open BookLibrary.Domain
open BookLibrary.Shared
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services
open BookLibrary.Utils
open Sharpino

type LibrarianChatService
    (
        textEmbeddingService: ITextEmbeddingService,
        vectorDbService: IVectorDbService,
        bookService: IBookService,
        authorService: IAuthorService,
        userTenantResolverService: IUserTenantResolverService,
        httpClient: HttpClient,
        apiKey: string,
        ?model: string
    ) =

    let modelName = defaultArg model GoogleGeminiHelpers.DefaultGeminiModel

    let callGemini (systemInstruction: string) (conversationTurns: (string * string) list) (userPrompt: string) (ct: CancellationToken) =
        task {
            try
                let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"

                let systemPart = {| parts = [| {| text = systemInstruction |} |] |}

                let historyParts =
                    conversationTurns
                    |> List.map (fun (role, text) ->
                        let geminiRole = if role = "assistant" then "model" else "user"
                        {| role = geminiRole; parts = [| {| text = text |} |] |}
                    )

                let currentTurn = {| role = "user"; parts = [| {| text = userPrompt |} |] |}
                let allContents = (historyParts @ [currentTurn]) |> List.toArray

                let requestBody =
                    {|
                        system_instruction = systemPart
                        contents = allContents
                    |}

                let jsonRequest = JsonSerializer.Serialize(requestBody)
                use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")

                let! response = httpClient.PostAsync(url, content, ct)

                if not response.IsSuccessStatusCode then
                    let! errorMsg = response.Content.ReadAsStringAsync(ct)
                    return Error $"Google Gemini API error: {response.StatusCode} - {errorMsg}"
                else
                    let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                    let result = JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)
                    return GoogleGeminiHelpers.extractTextFromCandidate result
            with ex ->
                return Error ex.Message
        }

    new
        (
            configuration: IConfiguration,
            httpClient: HttpClient,
            textEmbeddingService: ITextEmbeddingService,
            vectorDbService: IVectorDbService,
            bookService: IBookService,
            authorService: IAuthorService,
            userTenantResolverService: IUserTenantResolverService
        ) =
        let apiKey = configuration.GetValue<string>("GoogleVectorApiKey")
        if String.IsNullOrWhiteSpace apiKey then
            failwith "GoogleVectorApiKey is missing in configuration"
        let model = GoogleGeminiHelpers.resolveGeminiModel configuration None
        LibrarianChatService(textEmbeddingService, vectorDbService, bookService, authorService, userTenantResolverService, httpClient, apiKey, model)

    interface ILibrarianChatService with
        member this.ChatAsync(context: UserContext, request: LibrarianChatRequest, [<Optional; DefaultParameterValue(null: obj)>] ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None

            let buildTenantSearchInfo (tenant: Tenant) : TenantSearchInfo =
                let isPatron =
                    if context.IsAuthenticated && context.UserId.IsSome then
                        let uId = context.UserId.Value
                        tenant.OwnerId = uId || (tenant.Patrons |> List.exists (fun (p, _) -> p = uId))
                    else
                        false
                let requiresMembership = not tenant.TenantVisibility.IsPublic
                let canBorrow = isPatron || tenant.TenantVisibility.IsPublic
                let canReserve = isPatron || tenant.TenantVisibility.IsPublic

                {
                    TenantId = tenant.TenantId
                    TenantName = tenant.Name.Value
                    Address = tenant.Address
                    DistanceInMeters = None
                    Latitude = tenant.GeoLocSettings.OptCoordinates |> Option.map (fun c -> c.Latitude)
                    Longitude = tenant.GeoLocSettings.OptCoordinates |> Option.map (fun c -> c.Longitude)
                    IsUserPatron = isPatron
                    CanBorrowNow = canBorrow
                    CanReserveHold = canReserve
                    RequiresMembership = requiresMembership
                }

            taskResult {
                // Step 1: Determine whether to search anew or use existing ScopeBookIds
                let shouldPerformNewSearch =
                    request.ForceNewSearch
                    || (match request.ScopeBookIds with
                        | None -> true
                        | Some ids -> ids.IsEmpty)

                let! (booksWithScore: BookSearchResult list, matchedPaperIdsMap: System.Collections.Generic.IDictionary<BookId, Set<PaperId>>) =
                    taskResult {
                        if shouldPerformNewSearch then
                            let! targetTenantsList =
                                taskResult {
                                    match request.TargetTenantIds with
                                    | Some tIds when not tIds.IsEmpty ->
                                        return! userTenantResolverService.ResolveSearchTenantsAsync(context, TenantScope.ExplicitTenants tIds, ct)
                                    | _ ->
                                        let! tId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                                        return! userTenantResolverService.ResolveSearchTenantsAsync(context, TenantScope.ExplicitTenants [ tId ], ct)
                                }

                            let! embeddingResult = textEmbeddingService.GetEmbeddingAsync(context, request.Message, ct)

                            let perTenantTasks =
                                targetTenantsList
                                |> List.map (fun tenant -> task {
                                    let tId = tenant.TenantId
                                    let tenantContext = context.WithNewTenant(tId)
                                    let! detailedResults = vectorDbService.SearchSimilarDetailedAsync(embeddingResult, tId, 15, ?ct = Some ct)
                                    match detailedResults with
                                    | Ok details ->
                                        let detailedList = details |> Seq.toList
                                        let bookIds = detailedList |> List.map (fun r -> r.BookId) |> List.distinct
                                        if bookIds.IsEmpty then
                                            return Ok (tenant, [], detailedList)
                                        else
                                            let! booksRes = bookService.GetBooksAsync(tenantContext, bookIds, ct)
                                            match booksRes with
                                            | Ok books -> return Ok (tenant, books, detailedList)
                                            | Error err -> return Error err
                                    | Error err -> return Error err
                                })

                            let! perTenantResults = Task.WhenAll perTenantTasks
                            let perTenantSuccess =
                                perTenantResults
                                |> Array.choose (function Ok (t, books, details) -> Some (t, books, details) | Error _ -> None)
                                |> Array.toList

                            let matchedPaperIds =
                                perTenantSuccess
                                |> List.collect (fun (_, _, details) ->
                                    details
                                    |> List.choose (fun r -> r.PaperId |> Option.map (fun pid -> r.BookId, pid))
                                )
                                |> List.groupBy fst
                                |> List.map (fun (bId, pairs) -> bId, pairs |> List.map snd |> Set.ofList)
                                |> dict

                            let allResults =
                                perTenantSuccess
                                |> List.collect (fun (tenant, books, details) ->
                                    let tInfo = buildTenantSearchInfo tenant
                                    let bestScorePerBook =
                                        details
                                        |> List.groupBy (fun r -> r.BookId)
                                        |> List.map (fun (bId, group) -> bId, (group |> List.map (fun r -> r.Score) |> List.max))
                                        |> dict
                                    let booksDict = books |> List.distinctBy (fun b -> b.BookId) |> List.map (fun b -> b.BookId, b) |> dict
                                    booksDict.Keys
                                    |> Seq.map (fun bId ->
                                        {
                                            Book = booksDict.[bId]
                                            Score = if bestScorePerBook.ContainsKey(bId) then Some bestScorePerBook.[bId] else None
                                            Explanation = None
                                            TenantInfo = Some tInfo
                                        }
                                    )
                                    |> Seq.toList
                                )
                                |> List.sortByDescending (fun r -> r.Score |> Option.defaultValue 0.0)
                                |> List.truncate 15

                            return allResults, matchedPaperIds
                        else
                            let existingIds = request.ScopeBookIds.Value
                            let! targetTenantsList =
                                taskResult {
                                    match request.TargetTenantIds with
                                    | Some tIds when not tIds.IsEmpty ->
                                        return! userTenantResolverService.ResolveSearchTenantsAsync(context, TenantScope.ExplicitTenants tIds, ct)
                                    | _ ->
                                        let! tId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                                        return! userTenantResolverService.ResolveSearchTenantsAsync(context, TenantScope.ExplicitTenants [ tId ], ct)
                                }

                            let! allBooks =
                                targetTenantsList
                                |> List.map (fun tenant -> task {
                                    let tenantContext = context.WithNewTenant(tenant.TenantId)
                                    let! booksRes = bookService.GetBooksAsync(tenantContext, existingIds, ct)
                                    match booksRes with
                                    | Ok bList ->
                                        let tInfo = buildTenantSearchInfo tenant
                                        return bList |> List.map (fun b ->
                                            {
                                                Book = b
                                                Score = None
                                                Explanation = None
                                                TenantInfo = Some tInfo
                                            })
                                    | Error _ -> return []
                                })
                                |> Task.WhenAll
                            let results = allBooks |> Seq.concat |> Seq.distinctBy (fun r -> r.Book.BookId) |> Seq.toList
                            return results, dict []
                    }

                // Step 2: Enrich with author names (both book authors and paper authors) for high quality prompt context
                let allAuthorIds =
                    booksWithScore
                    |> List.collect (fun res ->
                        res.Book.Authors @ (res.Book.Papers |> List.collect (fun p -> p.Authors))
                    )
                    |> List.distinct

                let! (authorsMap: System.Collections.Generic.IDictionary<AuthorId, string>) =
                    task {
                        if allAuthorIds.IsEmpty then
                            return dict []
                        else
                            let! authorsRes = authorService.GetAuthorsAsync(context, allAuthorIds, ct)
                            match authorsRes with
                            | Ok authors ->
                                return authors |> List.map (fun a -> a.AuthorId, a.Name.Value) |> dict
                            | Error _ ->
                                return dict []
                    }

                // Step 3: Build catalog context text
                let booksContextSb = StringBuilder()
                if booksWithScore.IsEmpty then
                    booksContextSb.AppendLine("No matching books were found in the library catalog for this query.") |> ignore
                else
                    booksContextSb.AppendLine("AVAILABLE CATALOG BOOKS CONTEXT:") |> ignore
                    for res in booksWithScore do
                        let b = res.Book
                        let authorNames =
                            b.Authors
                            |> List.choose (fun aId -> if authorsMap.ContainsKey(aId) then Some authorsMap.[aId] else None)
                            |> String.concat ", "
                        let authorStr = if String.IsNullOrWhiteSpace authorNames then "Unknown" else authorNames
                        let desc = b.Description |> Option.defaultValue "No description provided."
                        let availability =
                            match b.AvailabilityStatus with
                            | AvailabilityStatus.Available -> "Available to borrow immediately"
                            | AvailabilityStatus.Reserved -> "Currently Reserved"
                            | AvailabilityStatus.NotAvailable -> "Not Available"
                            | AvailabilityStatus.Consultable -> "In-library consultation only"
                            | _ -> "Unspecified"

                        booksContextSb.AppendLine("---") |> ignore
                        booksContextSb.AppendLine($"[Book ID: {b.BookId.Value}]") |> ignore
                        match res.TenantInfo with
                        | Some tInfo ->
                            let addrStr = if String.IsNullOrWhiteSpace tInfo.Address then "" else $" ({tInfo.Address})"
                            booksContextSb.AppendLine($"Library / Collection: {tInfo.TenantName}{addrStr}") |> ignore
                        | None -> ()
                        booksContextSb.AppendLine($"Title: {b.Title.Value}") |> ignore
                        booksContextSb.AppendLine($"Authors: {authorStr}") |> ignore
                        booksContextSb.AppendLine($"Year: {b.Year.Value}") |> ignore
                        booksContextSb.AppendLine($"Category: {b.MainCategory.Value()}") |> ignore
                        booksContextSb.AppendLine($"Availability: {availability}") |> ignore
                        booksContextSb.AppendLine($"Synopsis: {desc}") |> ignore

                        if not (List.isEmpty b.Papers) then
                            booksContextSb.AppendLine("Collected Articles & Papers in this volume:") |> ignore
                            let matchedPids =
                                if matchedPaperIdsMap.ContainsKey(b.BookId) then
                                    matchedPaperIdsMap.[b.BookId]
                                else
                                    Set.empty

                            for p in b.Papers do
                                let isMatch = matchedPids.Contains(p.PaperId)
                                let matchTag = if isMatch then " [DIRECT RELEVANCE MATCH]" else ""
                                let pAuthors =
                                    p.Authors
                                    |> List.choose (fun aId -> if authorsMap.ContainsKey(aId) then Some authorsMap.[aId] else None)
                                    |> String.concat ", "
                                let pAuthorStr = if String.IsNullOrWhiteSpace pAuthors then "" else $" by {pAuthors}"
                                let pDesc =
                                    match p.Description with
                                    | Some d when not (String.IsNullOrWhiteSpace d) -> $"\n    Abstract: {d}"
                                    | _ -> ""
                                booksContextSb.AppendLine($"  * \"{p.Title.Value}\"{pAuthorStr}{matchTag}{pDesc}") |> ignore

                // Step 4: System Prompt with Mode Flag instructions
                let isMultiTenantMode =
                    match request.TargetTenantIds with
                    | Some tIds when tIds.Length > 1 -> true
                    | _ ->
                        let distinctTenants =
                            booksWithScore
                            |> List.choose (fun r -> r.TenantInfo |> Option.map (fun t -> t.TenantId))
                            |> List.distinct
                        distinctTenants.Length > 1

                let multiTenantInstruction =
                    if isMultiTenantMode then
                        """
MULTI-TENANT / MULTI-LIBRARY SPECIFICATION:
The user is searching across multiple libraries/tenants.
For EVERY book you mention, recommend, cite, or discuss from the catalog context, you MUST explicitly state in your response which library offers it, according to the "Library / Collection" field in the catalog context.
- Format example (English): "[Title](book://<book-guid>) is available at / offered by **<Library Name>**" or "(offered by **<Library Name>**)".
- Format example (Italian): "[Titolo](book://<book-guid>) è disponibile presso / offerto dalla biblioteca **<Nome Biblioteca>**" or "(offerto dalla biblioteca **<Nome Biblioteca>**)").
- Clearly distinguish and attribute books to their respective libraries so the patron knows where each book can be found.
"""
                    else
                        ""

                let generalKnowledgeInstruction =
                    if request.IncludeGeneralKnowledge then
                        """MODE: BROAD LITERARY & GENERAL KNOWLEDGE ENABLED.
You are permitted to use your broader literary, historical, and philosophical knowledge.
You may discuss books, authors, or literary movements outside the catalog context if relevant to the user's question.
IMPORTANT: When you mention or recommend any book that is in the catalog context, you MUST explicitly cite it with a catalog link markdown: [Book Title](book://<book-guid>) and clearly state that it is available in the user's library (specifying which library if in multi-tenant mode).
When mentioning external books that are NOT in the catalog, clearly mark them as (External / Not currently in library)."""
                    else
                        """MODE: STRICT CATALOG GROUNDING ONLY.
You MUST ONLY answer using the books provided in the AVAILABLE CATALOG BOOKS CONTEXT above.
Do NOT invent books, do NOT recommend books that are not in the catalog context.
If the user asks a question or looks for a topic not covered by the catalog books, politely clarify that the library catalog does not currently have titles on that specific topic, and point out the closest related titles available in the catalog context.
Always cite catalog books using markdown links: [Book Title](book://<book-guid>) and indicate which library offers each book when in multi-tenant mode."""

                let systemInstruction =
                    $"""You are the AI Librarian of BiblioNet, a thoughtful, articulate, and erudite literary advisor.
Your mission is to help patrons discover literature, analyze themes, compare books, and navigate the collection.

{generalKnowledgeInstruction}
{multiTenantInstruction}
CRITICAL CITATION RULES:
1. Whenever referring to a book from the catalog context, always link to it using this exact syntax: [Title](book://<book-guid>) where <book-guid> is the Book ID provided in the context (e.g. [Title](book://df2a54ad-021c-4aa1-8420-53356e4ffb9a)). NEVER omit the "book://" prefix (never write [Title](<guid>)).
2. Library attribution in multi-tenant mode: When searching across multiple libraries or when books from different libraries are present, you MUST explicitly specify for each book which library offers it (using the "Library / Collection" provided in the context).
3. Many volumes in the catalog contain collected articles, essays, or conference papers. When an article or paper within a book matches the patron's request or question (especially items marked [DIRECT RELEVANCE MATCH]), explicitly name the specific article, cite its authors if available, and indicate that it is published within [Title](book://<book-guid>).
4. If asked comparative questions (e.g., "which is the most ethically controversial?", "which is easiest for a beginner?", "compare the writing styles"), carefully evaluate the themes and descriptions of the provided books and give a clear, reasoned answer with comparative depth.
5. Respond in the same language as the user's message (e.g. if asked in Italian, respond in Italian; if English, respond in English).
6. Use clean Markdown formatting with clear sections, bullet points, or bold text for readability.

{booksContextSb.ToString()}
"""


                // Step 5: Format conversation history
                let conversationTurns =
                    request.ConversationHistory
                    |> List.map (fun m -> m.Role, m.Content)

                // Step 6: Call Gemini
                let! geminiAnswer = callGemini systemInstruction conversationTurns request.Message ct

                let finalScopeIds = booksWithScore |> List.map (fun r -> r.Book.BookId)

                return {
                    AnswerMarkdown = geminiAnswer
                    ReferencedBooks = booksWithScore
                    ScopeBookIds = finalScopeIds
                }
            }
