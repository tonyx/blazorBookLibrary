namespace BookLibrary.Services

open System.Threading
open System.Runtime.InteropServices
open System
open Sharpino
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Utils
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open BookLibrary.Services.UserMapping
open Sharpino.Cache
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open System.Net.Http
open System.Text
open System.Text.Json
open System.Text.Json.Serialization
open System.Text.RegularExpressions

[<CLIMutable>]
type GoogleEmbeddingValues = { values: float32[] }

[<CLIMutable>]
type GoogleEmbeddingResponse = { embedding: GoogleEmbeddingValues }

[<CLIMutable>]
type GeminiCandidateJson =
    {
        title: string option
        authors: string list option
        pageNumber: string option
        section: string option
        description: string option
    }

module TableOfContentsParser =
    let private pagePattern = Regex(@"(?:[\.\…\-_]{2,}|\s{3,})\s*(?:pag\.?|p\.?|pp\.?|page)?\s*(\d+)\s*$", RegexOptions.IgnoreCase)
    let private sectionPattern = Regex(@"^(?:SESSIONE|SESSION|PARTE|PART|SEZIONE|SECTION|CAPITOLO|CHAPTER)\b", RegexOptions.IgnoreCase)
    let private isDocHeader (line: string) =
        let trimmed = line.Trim().ToUpperInvariant()
        trimmed = "SOMMARIO" || trimmed = "INDICE" || trimmed = "TABLE OF CONTENTS" || trimmed = "INDEX" || trimmed = "CONTENTS"

    let private splitAuthors (text: string) : List<string> =
        if String.IsNullOrWhiteSpace text then []
        else
            let cleaned = Regex.Replace(text, @"\s+(?:e|and|&)\s+", ", ", RegexOptions.IgnoreCase)
            cleaned.Split([|','|], StringSplitOptions.RemoveEmptyEntries)
            |> Array.map (fun a -> a.Trim())
            |> Array.filter (fun a -> not (String.IsNullOrWhiteSpace a))
            |> Array.toList

    let parseText (text: string) : List<RecognizedPaperCandidate> =
        if String.IsNullOrWhiteSpace text then []
        else
            let lines = 
                text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n')
                |> Array.map (fun l -> l.Trim())
            
            let mutable currentSection = None
            let mutable currentBlockLines = []
            let candidates = ResizeArray<RecognizedPaperCandidate>()

            for line in lines do
                if String.IsNullOrWhiteSpace line then
                    ()
                elif isDocHeader line then
                    ()
                elif sectionPattern.IsMatch line || (line.Length > 3 && line = line.ToUpperInvariant() && not (pagePattern.IsMatch line)) then
                    currentSection <- Some line
                elif pagePattern.IsMatch line then
                    let m = pagePattern.Match line
                    let pageNum = m.Groups.[1].Value
                    let preText = line.Substring(0, m.Index).Trim()
                    
                    let title, authors =
                        if currentBlockLines.IsEmpty then
                            if preText.Contains(" — ") then
                                let parts = preText.Split([|" — "|], StringSplitOptions.None)
                                parts.[0].Trim(), splitAuthors parts.[1]
                            elif preText.Contains(" - ") then
                                let parts = preText.Split([|" - "|], StringSplitOptions.None)
                                parts.[0].Trim(), splitAuthors parts.[1]
                            elif preText.Contains(" / ") then
                                let parts = preText.Split([|" / "|], StringSplitOptions.None)
                                parts.[0].Trim(), splitAuthors parts.[1]
                            else
                                preText, []
                        else
                            let blockTitle = String.concat " " (List.rev currentBlockLines)
                            let hasInitialsOrComma = Regex.IsMatch(preText, @"[A-Z]\.\s*[A-Z]") || preText.Contains(",") || preText.Contains(" e ") || preText.Contains(" and ")
                            if hasInitialsOrComma || preText.Length < 40 then
                                blockTitle, splitAuthors preText
                            else
                                (blockTitle + " " + preText).Trim(), []

                    let desc =
                        match currentSection with
                        | Some s when not (String.IsNullOrWhiteSpace pageNum) -> Some $"{s} (pag. {pageNum})"
                        | Some s -> Some s
                        | None when not (String.IsNullOrWhiteSpace pageNum) -> Some $"pag. {pageNum}"
                        | None -> None

                    candidates.Add({
                        Title = title
                        Authors = authors
                        PageNumber = if String.IsNullOrWhiteSpace pageNum then None else Some $"pag. {pageNum}"
                        Section = currentSection
                        Description = desc
                    })
                    currentBlockLines <- []
                else
                    currentBlockLines <- line :: currentBlockLines

            candidates |> Seq.toList



type TextEmbeddingService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        httpClient: HttpClient,
        reviewViewerAsync: AggregateViewerAsync2<Review>,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        detailsService: IDetailsService,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        apiKey: string,
        userTenantResolverService: IUserTenantResolverService,
        ?model: string
    ) =

    let modelName = defaultArg model GoogleGeminiHelpers.DefaultGeminiModel

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            if context.IsInRole Role.Admin then
                return ()
            else
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
                return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    new
        (
            eventStore: IEventStore<string>,
            httpClient: HttpClient,
            detailsService: IDetailsService,
            apiKey: string,
            userTenantResolverService: IUserTenantResolverService,
            ?model: string
        ) =
        let messageSenders = MessageSenders.NoSender

        let reviewViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore

        let bookViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore

        let tenantViewerAsync =
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore

        TextEmbeddingService(
            eventStore,
            messageSenders,
            httpClient,
            reviewViewerAsync,
            bookViewerAsync,
            detailsService,
            tenantViewerAsync,
            apiKey,
            userTenantResolverService,
            ?model = model
        )

    new
        (
            configuration: IConfiguration,
            httpClient: HttpClient,
            detailsService: IDetailsService,
            secretsReader: SecretsReader,
            userTenantResolverService: IUserTenantResolverService
        ) =
        let apiKey = configuration.GetValue<string>("GoogleVectorApiKey")

        if String.IsNullOrWhiteSpace apiKey then
            failwith "GoogleVectorApiKey is missing in configuration"

        let model = GoogleGeminiHelpers.resolveGeminiModel configuration None

        let eventStore =
            PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString())

        TextEmbeddingService(eventStore, httpClient, detailsService, apiKey, userTenantResolverService, model)

    interface ITextEmbeddingService with
        member this.GetEmbeddingAsync
            (context: UserContext, text: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                try
                    let modelName = "models/gemini-embedding-2"

                    let url =
                        $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent?key={apiKey}"

                    let requestBody =
                        {| model = modelName
                           content = {| parts = [| {| text = text |} |] |}
                           output_dimensionality = 1536 |}

                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")

                    let! response = httpClient.PostAsync(url, content, ct)

                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync(ct)
                        return! Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                        let options = JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)

                        let result =
                            JsonSerializer.Deserialize<GoogleEmbeddingResponse>(jsonResponse, options)

                        return
                            { Model = modelName
                              Vector = result.embedding.values }
                with ex ->
                    return! Error ex.Message
            }

        member this.GetMatchExplanationAsync
            (
                context: UserContext,
                query: string,
                itemText: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                try
                    let url =
                        $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"

                    let prompt =
                        $"Explain concisely why the following query matches the provided text. Use the same language as the query.\n\nQuery: {query}\n\nText: {itemText}\n\nExplanation:"

                    let requestBody = {| contents = [| {| parts = [| {| text = prompt |} |] |} |] |}

                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")

                    let! response = httpClient.PostAsync(url, content, ct)

                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync(ct)
                        return! Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                        let result =
                            JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)

                        match GoogleGeminiHelpers.extractTextFromCandidate result with
                        | Ok text -> return text
                        | Error err -> return! Error err
                with ex ->
                    return! Error ex.Message
            }

        member this.GetBookDescriptionAsync
            (
                context: UserContext,
                bookData: PartialBookDataMatch,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                do! checkIsGlobalAdminOrTenantManager context ct

                try
                    let url =
                        $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"

                    let bookInfo =
                        match bookData with
                        | TitleAndAuthor(title, []) -> sprintf "Title: %s" title
                        | TitleAndAuthor(title, authors) ->
                            sprintf "Title: %s, Authors: %s" title (String.concat ", " authors)
                        | TitleAndIsbn(title, isbn) -> sprintf "Title: %s, ISBN: %s" title isbn
                        | AuthorAndIsbn(isbn, authors) ->
                            sprintf "ISBN: %s, Authors: %s" isbn (String.concat ", " authors)
                        | All(isbn, title, authors) ->
                            sprintf "Title: %s, Authors: %s, ISBN: %s" title (String.concat ", " authors) isbn

                    let prompt =
                        $"Provide a short, engaging description (max 150 words) for the following book to be used in a library catalogue. Focus on the plot summary or main themes. Use the same language as the book title if possible.\n\nBook Info: {bookInfo}\n\nDescription:"

                    let requestBody = {| contents = [| {| parts = [| {| text = prompt |} |] |} |] |}

                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")

                    let! response = httpClient.PostAsync(url, content, ct)

                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync(ct)
                        return! Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                        let result =
                            JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)

                        match GoogleGeminiHelpers.extractTextFromCandidate result with
                        | Ok text -> return text
                        | Error err -> return! Error err
                with ex ->
                    return! Error ex.Message
            }


        member this.GetPartialBookMatchByCoverImage
            (
                context: UserContext,
                base64Image: string,
                mimeType: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                do! checkIsGlobalAdminOrTenantManager context ct

                try
                    let url =
                        $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"

                    let prompt =
                        "Identify the book from this cover image. Provide the title, authors (as a list), and the most likely ISBN-13 for this book. Use your internal knowledge to provide the ISBN even if it is not explicitly printed on the cover image. Format the response as a JSON object with the following keys: 'title', 'authors', 'isbn'."

                    let requestBody =
                        {| contents =
                            [| {| parts =
                                   [| box {| text = prompt |}
                                      box
                                          {| inline_data =
                                              {| mime_type = mimeType
                                                 data = base64Image |} |} |] |} |]
                           generationConfig =
                               {| response_mime_type = "application/json"
                                  thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}

                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")

                    let! response = httpClient.PostAsync(url, content, ct)

                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync(ct)
                        return! Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                        let options = JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)

                        let result =
                            JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)

                        match GoogleGeminiHelpers.extractTextFromCandidate result with
                        | Error err -> return! Error err
                        | Ok rawText ->
                            let textResponse = GoogleGeminiHelpers.cleanJsonCodeBlock rawText
                            try
                                // Use an intermediate record with options to handle potential nulls from Gemini
                                let tempResult =
                                    JsonSerializer.Deserialize<
                                        {| title: string option
                                           authors: string list option
                                           isbn: string option |}
                                     >(
                                        textResponse,
                                        options
                                    )

                                let title = tempResult.title |> Option.defaultValue ""
                                let authors = tempResult.authors |> Option.defaultValue []
                                let isbn = tempResult.isbn |> Option.defaultValue ""

                                let hasIsbn = not (String.IsNullOrWhiteSpace isbn)
                                let hasTitle = not (String.IsNullOrWhiteSpace title)
                                let hasAuthors = not (authors |> List.isEmpty)

                                match hasIsbn, hasTitle, hasAuthors with
                                | true, true, true -> return (All(isbn, title, authors))
                                | true, true, false -> return (TitleAndIsbn(title, isbn))
                                | true, false, true -> return (AuthorAndIsbn(isbn, authors))
                                | true, false, false -> return (AuthorAndIsbn(isbn, []))
                                | false, true, true -> return (TitleAndAuthor(title, authors))
                                | false, true, false -> return (TitleAndAuthor(title, []))
                                | _ ->
                                    return!
                                        Error
                                            $"Could not identify enough book data from the cover. Raw response: {textResponse}"
                            with ex ->
                                return!
                                    Error
                                        $"Failed to parse Gemini JSON response: {ex.Message}. Response was: {textResponse}"
                with ex ->
                    return! Error ex.Message
            }

        member this.RecognizePapersFromImageAsync
            (
                context: UserContext,
                base64Image: string,
                mimeType: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                do! checkIsGlobalAdminOrTenantManager context ct

                try
                    let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"
                    let prompt =
                        "Extract all articles, papers, or chapters from this scanned table of contents / sommario / index image. For each item return a JSON object with: 'title' (full title of the paper/article, clean of page numbers or author names), 'authors' (array of author full names as strings, or empty array if none), 'pageNumber' (the page string e.g. 'pag. 15', or null), 'section' (the session or chapter heading under which it appears, or null), 'description' (any relevant contextual note or session info e.g. 'SESSIONE I (pag. 15)', or null). Return a JSON array of these objects only."

                    let requestBody =
                        {| contents =
                            [| {| parts =
                                   [| box {| text = prompt |}
                                      box
                                          {| inline_data =
                                              {| mime_type = mimeType
                                                 data = base64Image |} |} |] |} |]
                           generationConfig =
                               {| response_mime_type = "application/json"
                                  thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}

                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                    let! response = httpClient.PostAsync(url, content, ct)

                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync(ct)
                        return! Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                        let options = JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)
                        let genResult = JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)

                        match GoogleGeminiHelpers.extractTextFromCandidate genResult with
                        | Error err -> return! Error err
                        | Ok rawText ->
                            let textPart = GoogleGeminiHelpers.cleanJsonCodeBlock rawText
                            let rawCandidates = JsonSerializer.Deserialize<GeminiCandidateJson list>(textPart, options)
                            let results =
                                rawCandidates
                                |> List.map (fun c ->
                                    {
                                        Title = c.title |> Option.defaultValue ""
                                        Authors = c.authors |> Option.defaultValue []
                                        PageNumber = c.pageNumber
                                        Section = c.section
                                        Description = c.description
                                    })
                                |> List.filter (fun c -> not (String.IsNullOrWhiteSpace c.Title))
                            return results
                with ex ->
                    return! Error ex.Message
            }

        member this.RecognizePapersFromTextAsync
            (
                context: UserContext,
                text: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                do! checkIsGlobalAdminOrTenantManager context ct

                if String.IsNullOrWhiteSpace text then
                    return []
                else
                    let canUseGemini = not (String.IsNullOrWhiteSpace apiKey) && not (apiKey.StartsWith "dummy")
                    if canUseGemini then
                        try
                            let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"
                            let prompt =
                                "Extract all articles, papers, or chapters from this table of contents / sommario / index text. For each item return a JSON object with: 'title' (full title of the paper/article, clean of page numbers or author names), 'authors' (array of author full names as strings, or empty array if none), 'pageNumber' (the page string e.g. 'pag. 15', or null), 'section' (the session or chapter heading under which it appears, or null), 'description' (any relevant contextual note or session info e.g. 'SESSIONE I (pag. 15)', or null). Return a JSON array of these objects only."

                            let requestBody =
                                {| contents = [| {| parts = [| box {| text = $"{prompt}\n\n{text}" |} |] |} |]
                                   generationConfig =
                                       {| response_mime_type = "application/json"
                                          thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}

                            let jsonRequest = JsonSerializer.Serialize(requestBody)
                            use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                            let! response = httpClient.PostAsync(url, content, ct)
                            if response.IsSuccessStatusCode then
                                let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                                let options = JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)
                                let genResult = JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)
                                match GoogleGeminiHelpers.extractTextFromCandidate genResult with
                                | Error _ -> return TableOfContentsParser.parseText text
                                | Ok rawText ->
                                    let textPart = GoogleGeminiHelpers.cleanJsonCodeBlock rawText
                                    let rawCandidates = JsonSerializer.Deserialize<GeminiCandidateJson list>(textPart, options)

                                    let results =
                                        rawCandidates
                                        |> List.map (fun c ->
                                            {
                                                Title = c.title |> Option.defaultValue ""
                                                Authors = c.authors |> Option.defaultValue []
                                                PageNumber = c.pageNumber
                                                Section = c.section
                                                Description = c.description
                                            })
                                        |> List.filter (fun c -> not (String.IsNullOrWhiteSpace c.Title))
                                    if not (List.isEmpty results) then
                                        return results
                                    else
                                        return TableOfContentsParser.parseText text
                            else
                                return TableOfContentsParser.parseText text
                        with _ ->
                            return TableOfContentsParser.parseText text
                    else
                        return TableOfContentsParser.parseText text
            }

        member this.ExtractTextFromImageAsync
            (
                context: UserContext,
                base64Image: string,
                mimeType: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            taskResult {
                do! checkIsGlobalAdminOrTenantManager context ct

                try
                    let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"
                    let prompt =
                        "Transcribe the text from this image faithfully and completely, preserving paragraphs, headings, and formatting. Do not add conversational remarks or commentary. Output only the transcribed text."

                    let requestBody =
                        {| contents =
                            [| {| parts =
                                   [| box {| text = prompt |}
                                      box
                                          {| inline_data =
                                              {| mime_type = mimeType
                                                 data = base64Image |} |} |] |} |]
                           generationConfig =
                               {| thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}

                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                    let! response = httpClient.PostAsync(url, content, ct)

                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync(ct)
                        return! Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                        let genResult = JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)

                        match GoogleGeminiHelpers.extractTextFromCandidate genResult with
                        | Error err -> return! Error err
                        | Ok text -> return text.Trim()
                with ex ->
                    return! Error ex.Message
            }

