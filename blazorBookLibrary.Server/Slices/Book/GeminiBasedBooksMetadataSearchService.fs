namespace BookLibrary.Services

open System
open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open System.Text.Json.Serialization
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open Sharpino
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Utils
open Sharpino.CommandHandler
open FsToolkit.ErrorHandling
open Sharpino.EventBroker
open System.Text.Json
open System.Text.Json.Serialization
open System.Collections.Generic
open System.Threading
open System.Runtime.InteropServices
open Sharpino
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Utils
open Sharpino.CommandHandler
open FsToolkit.ErrorHandling
open Sharpino.EventBroker

[<AllowNullLiteral>]
type GooglePart() =
    [<JsonPropertyName("text")>]
    member val text: string = "" with get, set

    [<JsonPropertyName("thought")>]
    member val thought: Nullable<bool> = Nullable() with get, set

 [<AllowNullLiteral>]
type GoogleContent() =
    [<JsonPropertyName("parts")>]
    member val parts: GooglePart[] = [||] with get, set

[<AllowNullLiteral>]
type GoogleCandidate() =
    [<JsonPropertyName("content")>]
    member val content: GoogleContent = null with get, set

[<AllowNullLiteral>]
type GoogleGenerateResponse() =
    [<JsonPropertyName("candidates")>]
    member val candidates: GoogleCandidate[] = [||] with get, set

module GoogleGeminiHelpers =
    [<Literal>]
    let DefaultGeminiModel = "gemini-3.6-flash"

    let geminiJsonOptions =
        let opt = System.Text.Json.JsonSerializerOptions(PropertyNameCaseInsensitive = true, AllowTrailingCommas = true)
        opt.ReadCommentHandling <- System.Text.Json.JsonCommentHandling.Skip
        opt

    let resolveGeminiModel (configuration: IConfiguration) (overrideModel: string option) : string =
        match overrideModel with
        | Some m when not (String.IsNullOrWhiteSpace m) -> m
        | _ ->
            if isNull (box configuration) then
                DefaultGeminiModel
            else
                let configured = configuration.GetValue<string>("GoogleGeminiModel")
                if not (String.IsNullOrWhiteSpace configured) then configured else DefaultGeminiModel

    let cleanJsonCodeBlock (rawJsonText: string) : string =
        if String.IsNullOrWhiteSpace rawJsonText then ""
        else
            let cleaned =
                rawJsonText.Trim()
                    .Replace("```json", "")
                    .Replace("```JSON", "")
                    .Replace("```", "")
                    .Trim()
            let firstBrace = cleaned.IndexOfAny([| '{'; '[' |])
            let lastBrace = cleaned.LastIndexOfAny([| '}'; ']' |])
            if firstBrace >= 0 && lastBrace >= firstBrace then
                cleaned.Substring(firstBrace, lastBrace - firstBrace + 1)
            else
                cleaned

    let extractTextFromCandidate (response: GoogleGenerateResponse) : Result<string, string> =
        if isNull (box response) || isNull (box response.candidates) || response.candidates.Length = 0 then
            Error "Failed to receive a valid response from Gemini."
        else
            let candidate = response.candidates.[0]
            if isNull (box candidate) || isNull (box candidate.content) || isNull (box candidate.content.parts) || candidate.content.parts.Length = 0 then
                Error "Failed to receive valid content parts from Gemini."
            else
                let textParts =
                    candidate.content.parts
                    |> Array.filter (fun p ->
                        not (isNull (box p))
                        && not (p.thought.HasValue && p.thought.Value)
                        && not (String.IsNullOrWhiteSpace p.text))
                    |> Array.map (fun p -> p.text)

                let combinedText =
                    if textParts.Length > 0 then
                        String.concat "" textParts
                    else
                        candidate.content.parts
                        |> Array.choose (fun p -> if isNull (box p) || String.IsNullOrWhiteSpace p.text then None else Some p.text)
                        |> String.concat ""

                if String.IsNullOrWhiteSpace combinedText then
                    Error "Empty text content returned from Gemini."
                else
                    Ok (combinedText.Trim())

    let calculateIsbn13CheckDigit (digits: string) =
        let sum =
            digits
            |> Seq.take 12
            |> Seq.mapi (fun i c ->
                let v = int c - int '0'
                v * (if i % 2 = 0 then 1 else 3))
            |> Seq.sum
        let rem = sum % 10
        let check = (10 - rem) % 10
        string check

    let calculateIsbn10CheckDigit (digits: string) =
        let sum =
            digits
            |> Seq.take 9
            |> Seq.mapi (fun i c ->
                let v = int c - int '0'
                v * (10 - i))
            |> Seq.sum
        let rem = sum % 11
        let check = (11 - rem) % 11
        if check = 10 then "X" else string check

    let tryCorrectIsbn (isbn: string) =
        if String.IsNullOrWhiteSpace isbn then
            isbn
        else
            let clean = isbn.Replace("-", "").Replace(" ", "")
            if clean.Length = 13 && (clean |> Seq.take 12 |> Seq.forall Char.IsDigit) then
                let correctDigit = calculateIsbn13CheckDigit clean
                clean.Substring(0, 12) + correctDigit
            elif clean.Length = 10 && (clean |> Seq.take 9 |> Seq.forall Char.IsDigit) then
                let correctDigit = calculateIsbn10CheckDigit clean
                clean.Substring(0, 9) + correctDigit
            else
                isbn

    let tryGetProperty (names: string list) (elem: JsonElement) : JsonElement option =
        if elem.ValueKind <> JsonValueKind.Object then None
        else
            let props = elem.EnumerateObject()
            names
            |> List.tryPick (fun targetName ->
                props
                |> Seq.tryFind (fun p -> String.Equals(p.Name, targetName, StringComparison.OrdinalIgnoreCase))
                |> Option.map (fun p -> p.Value)
                |> Option.filter (fun v -> v.ValueKind <> JsonValueKind.Null && v.ValueKind <> JsonValueKind.Undefined))

    let parseBookElement (elem: JsonElement) : GoogleBookMetadata =
        let title =
            match tryGetProperty [ "title"; "bookTitle"; "name" ] elem with
            | Some v when v.ValueKind = JsonValueKind.String -> v.GetString().Trim()
            | _ -> ""

        let authors =
            match tryGetProperty [ "authors"; "author"; "writers"; "writer" ] elem with
            | Some v when v.ValueKind = JsonValueKind.Array ->
                v.EnumerateArray()
                |> Seq.choose (fun item ->
                    if item.ValueKind = JsonValueKind.String then
                        let s = item.GetString()
                        if String.IsNullOrWhiteSpace s then None else Some (s.Trim())
                    else None)
                |> Seq.toList
            | Some v when v.ValueKind = JsonValueKind.String ->
                let s = v.GetString().Trim()
                if String.IsNullOrWhiteSpace s then [] else [ s ]
            | _ -> []

        let categories =
            match tryGetProperty [ "categories"; "category"; "genres"; "genre" ] elem with
            | Some v when v.ValueKind = JsonValueKind.Array ->
                v.EnumerateArray()
                |> Seq.choose (fun item ->
                    if item.ValueKind = JsonValueKind.String then
                        let s = item.GetString()
                        if String.IsNullOrWhiteSpace s then None else Some (s.Trim())
                    else None)
                |> Seq.toList
            | Some v when v.ValueKind = JsonValueKind.String ->
                let s = v.GetString().Trim()
                if String.IsNullOrWhiteSpace s then []
                else
                    s.Split([| ','; ';' |], StringSplitOptions.RemoveEmptyEntries)
                    |> Seq.map (fun x -> x.Trim())
                    |> Seq.filter (fun x -> not (String.IsNullOrWhiteSpace x))
                    |> Seq.toList
            | _ -> []

        let year =
            match tryGetProperty [ "year"; "publishedYear"; "publicationYear"; "published_year"; "date" ] elem with
            | Some v when v.ValueKind = JsonValueKind.Number ->
                try
                    Some (v.GetInt32())
                with _ -> None
            | Some v when v.ValueKind = JsonValueKind.String ->
                let s = v.GetString().Trim()
                let m = System.Text.RegularExpressions.Regex.Match(s, @"\b(19\d\d|20\d\d)\b")
                if m.Success then
                    match Int32.TryParse(m.Value) with
                    | true, y -> Some y
                    | false, _ -> None
                else
                    match Int32.TryParse(s) with
                    | true, y -> Some y
                    | false, _ -> None
            | _ -> None

        let isbn =
            match tryGetProperty [ "isbn"; "isbn13"; "isbn10"; "isbn_13"; "isbn_10" ] elem with
            | Some v when v.ValueKind = JsonValueKind.String ->
                let s = v.GetString().Trim()
                if String.IsNullOrWhiteSpace s then None else Some (tryCorrectIsbn s)
            | _ -> None

        let description =
            match tryGetProperty [ "description"; "summary"; "synopsis"; "overview" ] elem with
            | Some v when v.ValueKind = JsonValueKind.String ->
                let s = v.GetString().Trim()
                if String.IsNullOrWhiteSpace s then None else Some s
            | _ -> None

        { Title = title
          Authors = List<string>(authors)
          Categories = List<string>(categories)
          Year = year
          Isbn = isbn
          Description = description }

    let parseBooksMetadataList (jsonStr: string) : GoogleBookMetadata list =
        use doc = JsonDocument.Parse(jsonStr)
        let root = doc.RootElement
        match root.ValueKind with
        | JsonValueKind.Array ->
            root.EnumerateArray()
            |> Seq.filter (fun e -> e.ValueKind = JsonValueKind.Object)
            |> Seq.map parseBookElement
            |> Seq.toList
        | JsonValueKind.Object ->
            let innerArray =
                [ "books"; "items"; "results"; "data" ]
                |> List.tryPick (fun name ->
                    let props = root.EnumerateObject()
                    props
                    |> Seq.tryFind (fun p -> String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    |> Option.map (fun p -> p.Value)
                    |> Option.filter (fun v -> v.ValueKind = JsonValueKind.Array))
            match innerArray with
            | Some arr ->
                arr.EnumerateArray()
                |> Seq.filter (fun e -> e.ValueKind = JsonValueKind.Object)
                |> Seq.map parseBookElement
                |> Seq.toList
            | None ->
                [ parseBookElement root ]
        | _ -> []

    let parseSingleBookMetadata (jsonStr: string) : GoogleBookMetadata option =
        use doc = JsonDocument.Parse(jsonStr)
        let root = doc.RootElement
        match root.ValueKind with
        | JsonValueKind.Object ->
            let innerArray =
                [ "books"; "items"; "results"; "data" ]
                |> List.tryPick (fun name ->
                    let props = root.EnumerateObject()
                    props
                    |> Seq.tryFind (fun p -> String.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                    |> Option.map (fun p -> p.Value)
                    |> Option.filter (fun v -> v.ValueKind = JsonValueKind.Array))
            match innerArray with
            | Some arr ->
                arr.EnumerateArray()
                |> Seq.filter (fun e -> e.ValueKind = JsonValueKind.Object)
                |> Seq.map parseBookElement
                |> Seq.tryHead
            | None ->
                Some (parseBookElement root)
        | JsonValueKind.Array ->
            root.EnumerateArray()
            |> Seq.filter (fun e -> e.ValueKind = JsonValueKind.Object)
            |> Seq.map parseBookElement
            |> Seq.tryHead
        | _ -> None

[<CLIMutable>]
type GeminiBookMetadataResponse =
    { title: string
      authors: string[]
      categories: string[]
      year: Nullable<int>
      isbn: string
      description: string }

type GeminiBasedBooksMetadataSearchService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        secretsReader: SecretsReader,
        httpClient: HttpClient,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userTenantResolverService: IUserTenantResolverService,
        apiKey: string,
        ?model: string
    ) =

    let modelName = defaultArg model GoogleGeminiHelpers.DefaultGeminiModel
    let calculateIsbn13CheckDigit = GoogleGeminiHelpers.calculateIsbn13CheckDigit
    let calculateIsbn10CheckDigit = GoogleGeminiHelpers.calculateIsbn10CheckDigit
    let tryCorrectIsbn = GoogleGeminiHelpers.tryCorrectIsbn

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    let tenantViewer =
        getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore

    let toGoogleBookMetadata (r: GeminiBookMetadataResponse) =
        { Title = if isNull r.title then "" else r.title
          Authors = List<string>(if isNull r.authors then [||] else r.authors)
          Categories = List<string>(if isNull r.categories then [||] else r.categories)
          Year = if r.year.HasValue then Some r.year.Value else None
          Isbn = if String.IsNullOrWhiteSpace r.isbn then None else Some r.isbn
          Description = if String.IsNullOrWhiteSpace r.description then None else Some r.description }

    let callGemini (prompt: string) (useGoogleSearch: bool) (responseMimeType: string option) (ct: CancellationToken) =
        task {
            try
                let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"

                let requestBody =
                    if useGoogleSearch then
                        box {| contents = [| {| parts = [| {| text = prompt |} |] |} |]
                               tools = [| {| google_search = {| |} |} |]
                               generationConfig =
                                   {| thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}
                    else
                        match responseMimeType with
                        | Some mimeType ->
                            box {| contents = [| {| parts = [| {| text = prompt |} |] |} |]
                                   generationConfig =
                                       {| response_mime_type = mimeType
                                          thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}
                        | None ->
                            box {| contents = [| {| parts = [| {| text = prompt |} |] |} |]
                                   generationConfig =
                                       {| thinkingConfig = {| thinkingLevel = "MINIMAL" |} |} |}

                let jsonRequest = System.Text.Json.JsonSerializer.Serialize(requestBody)
                use content = new StringContent(jsonRequest, System.Text.Encoding.UTF8, "application/json")

                let! response = httpClient.PostAsync(url, content, ct)

                if not response.IsSuccessStatusCode then
                    let! errorMsg = response.Content.ReadAsStringAsync(ct)
                    return Error $"Google API error: {response.StatusCode} - {errorMsg}"
                else
                    let! jsonResponse = response.Content.ReadAsStringAsync(ct)
                    let result = System.Text.Json.JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, GoogleGeminiHelpers.geminiJsonOptions)
                    return GoogleGeminiHelpers.extractTextFromCandidate result
            with ex ->
                return Error ex.Message
        }

    new
        (
            configuration: IConfiguration,
            messageSenders: MessageSenders,
            secretsReader: SecretsReader,
            httpClient: HttpClient,
            userTenantResolverService: IUserTenantResolverService
        ) =
        let apiKey = configuration.GetValue<string>("GoogleVectorApiKey")
        let model = GoogleGeminiHelpers.resolveGeminiModel configuration None

        let eventStore =
            PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString())

        let tenantViewer =
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> eventStore

        GeminiBasedBooksMetadataSearchService(
            eventStore,
            messageSenders,
            secretsReader,
            httpClient,
            tenantViewer,
            userTenantResolverService,
            apiKey,
            model
        )

    interface IBooksMetadataSearchService with
        member this.LookupByIsbnAsync
            (context: UserContext, isbn: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            task {
                let! allowed = checkIsGlobalAdminOrTenantManager context ct
                if allowed |> Result.isError then
                    return Error "Not authorized to lookup books"
                else
                    let cleanIsbn = isbn.Replace("-", "").Replace(" ", "").Trim()
                    let prompt =
                        $"Find the metadata for the book with ISBN: {cleanIsbn}. Search the web for this ISBN to identify the exact book title, author, year, categories, and description. Format the response as a single JSON object with the following keys: 'title' (string), 'authors' (array of strings, do NOT use 'author'), 'categories' (array of strings), 'year' (integer, or null if unknown), 'isbn' (string), and 'description' (string, a brief summary of the book, or null if unknown)."
                    let! responseResult = callGemini prompt true None ct
                    match responseResult with
                    | Ok rawJsonStr ->
                        try
                            let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                            match GoogleGeminiHelpers.parseSingleBookMetadata jsonStr with
                            | Some metadata ->
                                let correctedIsbn =
                                    match metadata.Isbn with
                                    | Some i when not (String.IsNullOrWhiteSpace i) -> tryCorrectIsbn i
                                    | _ -> cleanIsbn
                                let metadataWithActualIsbn = { metadata with Isbn = Some correctedIsbn }
                                return Ok (Some metadataWithActualIsbn)
                            | None ->
                                return Ok None
                        with ex ->
                            return Error $"Failed to parse Gemini metadata: {ex.Message}. Response was: {rawJsonStr}"
                    | Error err ->
                        return Error err
            }

        member this.LookupByTitleAsync
            (context: UserContext, title: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            task {
                let! allowed = checkIsGlobalAdminOrTenantManager context ct
                if allowed |> Result.isError then
                    return Error "Not authorized to lookup books"
                else
                    let prompt =
                        $"Find the metadata for the book with title: {title}. Search the web to retrieve highly accurate information and identify its correct ISBN-13. Format the response as a single JSON object with the following keys: 'title' (string), 'authors' (array of strings, do NOT use 'author'), 'categories' (array of strings), 'year' (integer, or null if unknown), 'isbn' (string), and 'description' (string, a brief summary of the book, or null if unknown)."
                    let! responseResult = callGemini prompt true None ct
                    match responseResult with
                    | Ok rawJsonStr ->
                        try
                            let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                            match GoogleGeminiHelpers.parseSingleBookMetadata jsonStr with
                            | Some metadata ->
                                let correctedIsbn =
                                    match metadata.Isbn with
                                    | Some i when not (String.IsNullOrWhiteSpace i) -> Some (tryCorrectIsbn i)
                                    | _ -> None
                                let metadataWithCorrectedIsbn = { metadata with Isbn = correctedIsbn }
                                return Ok (Some metadataWithCorrectedIsbn)
                            | None ->
                                return Ok None
                        with ex ->
                            return Error $"Failed to parse Gemini metadata: {ex.Message}. Response was: {rawJsonStr}"
                    | Error err ->
                        return Error err
            }

        member this.LookupMultipleByTitleAsync
            (context: UserContext, title: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            task {
                let! allowed = checkIsGlobalAdminOrTenantManager context ct
                if allowed |> Result.isError then
                    return Error "Not authorized to lookup books"
                else
                    let prompt =
                        $"Find metadata for up to 3 books that match or are highly relevant to the search title: {title}. Search the web to identify the correct ISBN-13 for each book and ensure it is valid. Format the response as a JSON array of objects, where each object has the keys: 'title' (string), 'authors' (array of strings, do NOT use 'author'), 'categories' (array of strings), 'year' (integer, or null if unknown), 'isbn' (string, or null if unknown), and 'description' (string, or null if unknown)."
                    let! responseResult = callGemini prompt true None ct
                    match responseResult with
                    | Ok rawJsonStr ->
                        try
                            let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                            let results = GoogleGeminiHelpers.parseBooksMetadataList jsonStr
                            let correctedResults =
                                results
                                |> List.map (fun m ->
                                    let correctedIsbn =
                                        match m.Isbn with
                                        | Some i when not (String.IsNullOrWhiteSpace i) -> Some (tryCorrectIsbn i)
                                        | _ -> None
                                    { m with Isbn = correctedIsbn })
                            return Ok correctedResults
                        with ex ->
                            return Error $"Failed to parse Gemini metadata list: {ex.Message}. Response was: {rawJsonStr}"
                    | Error err ->
                        return Error err
            }

        member this.LookupCoverImageByIsbnAsync
            (
                context: UserContext,
                isbn: Isbn,
                [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            let size = defaultArg thumbRoughSize ThumbRoughSize.Medium
            let sizeStr = size.ShortPrint
            task {
                let! allowed = checkIsGlobalAdminOrTenantManager context ct
                if allowed |> Result.isError then
                    return Error "Not authorized to lookup book covers"
                else
                    match isbn with
                    | Isbn value ->
                        let correctedValue = tryCorrectIsbn value
                        let url = $"https://covers.openlibrary.org/b/isbn/{correctedValue}-{sizeStr}.jpg"
                        let! verified = BookCoverHelpers.verifyCoverUrl httpClient url ct
                        match verified with
                        | Some v -> return Ok (Some v)
                        | None ->
                            let! fromEdition = BookCoverHelpers.checkOpenLibraryEditionCovers httpClient correctedValue sizeStr ct
                            return Ok fromEdition
                    | InvalidIsbn _ -> return Error "Cannot lookup cover for an invalid ISBN."
                    | EmptyIsbn -> return Ok None
            }

        member this.LookupGoogleApiCoverImageByIsbnAsync
            (context: UserContext, isbn: Isbn, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            (this :> IBooksMetadataSearchService).LookupCoverImageByIsbnAsync(context, isbn, ThumbRoughSize.Medium, ct)

        member this.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync
            (
                context: UserContext,
                isbn: Isbn,
                [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            let size = defaultArg thumbRoughSize ThumbRoughSize.Medium
            task {
                let! allowed = checkIsGlobalAdminOrTenantManager context ct
                if allowed |> Result.isError then
                    return Error "Not authorized to lookup book covers"
                else
                    let! openLibraryResult =
                        (this :> IBooksMetadataSearchService).LookupCoverImageByIsbnAsync(context, isbn, size, ct)

                    match openLibraryResult with
                    | Ok(Some url) -> return Ok(Some url)
                    | _ ->
                        return!
                            (this :> IBooksMetadataSearchService)
                                .LookupGoogleApiCoverImageByIsbnAsync(context, isbn, ct)
            }

        member this.LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync
            (
                context: UserContext,
                title: string,
                [<Optional; DefaultParameterValue(null)>] ?author: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            task {
                let authorPart = match author with Some a when not (String.IsNullOrWhiteSpace a) -> $" by {a}" | _ -> ""
                let prompt =
                    $"Find the most likely, accurate ISBN-13 for the book with title: '{title}'{authorPart}. Search the web to find the real ISBN-13. Format the response as a single JSON object with a single key 'isbn' (string)."
                let! responseResult = callGemini prompt true None ct
                match responseResult with
                | Ok rawJsonStr ->
                    try
                        let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                        use doc = JsonDocument.Parse(jsonStr)
                        let root =
                            if doc.RootElement.ValueKind = JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0 then
                                doc.RootElement.[0]
                            else
                                doc.RootElement
                        let isbnOpt =
                            if root.ValueKind = JsonValueKind.Object then
                                GoogleGeminiHelpers.tryGetProperty [ "isbn"; "isbn13"; "isbn10"; "isbn_13"; "isbn_10" ] root
                                |> Option.bind (fun v ->
                                    if v.ValueKind = JsonValueKind.String then
                                        let s = v.GetString().Trim()
                                        if String.IsNullOrWhiteSpace s then None else Some s
                                    else None)
                            else None
                        match isbnOpt with
                        | Some isbnVal ->
                            let correctedIsbn = tryCorrectIsbn isbnVal
                            let url = $"https://covers.openlibrary.org/b/isbn/{correctedIsbn}-M.jpg"
                            let! verified = BookCoverHelpers.verifyCoverUrl httpClient url ct
                            match verified with
                            | Some v -> return Ok (Some v)
                            | None ->
                                let! fromEdition = BookCoverHelpers.checkOpenLibraryEditionCovers httpClient correctedIsbn "M" ct
                                match fromEdition with
                                | Some v -> return Ok (Some v)
                                | None ->
                                    let! openLibResult = BookCoverHelpers.searchOpenLibraryCover httpClient title author "M" ct
                                    return Ok openLibResult
                        | None ->
                            let! openLibResult = BookCoverHelpers.searchOpenLibraryCover httpClient title author "M" ct
                            return Ok openLibResult
                    with _ ->
                        let! openLibResult = BookCoverHelpers.searchOpenLibraryCover httpClient title author "M" ct
                        return Ok openLibResult
                | Error _ ->
                    let! openLibResult = BookCoverHelpers.searchOpenLibraryCover httpClient title author "M" ct
                    return Ok openLibResult
            }
