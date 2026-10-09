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
open System.Text.Json.Serialization

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
            rawJsonText.Trim()
                .Replace("```json", "")
                .Replace("```JSON", "")
                .Replace("```", "")
                .Trim()

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

    let callGemini (prompt: string) (responseMimeType: string option) (ct: CancellationToken) =
        task {
            try
                let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"

                let requestBody =
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
                    let prompt =
                        $"Find the metadata for the book with ISBN: {isbn}. Use your internal knowledge to retrieve highly accurate information. You MUST ensure the 'isbn' returned is a mathematically valid 13-digit or 10-digit ISBN number. Format the response as a single JSON object with the following keys: 'title' (string), 'authors' (array of strings), 'categories' (array of strings), 'year' (integer, or null if unknown), 'isbn' (string), and 'description' (string, a brief summary of the book, or null if unknown)."
                    let! responseResult = callGemini prompt (Some "application/json") ct
                    match responseResult with
                    | Ok rawJsonStr ->
                        try
                            let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                            let options = System.Text.Json.JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)
                            let res = System.Text.Json.JsonSerializer.Deserialize<GeminiBookMetadataResponse>(jsonStr, options)
                            let correctedIsbn = if not (String.IsNullOrWhiteSpace res.isbn) then tryCorrectIsbn res.isbn else isbn
                            let correctedRes = { res with isbn = correctedIsbn }
                            let metadata = toGoogleBookMetadata correctedRes
                            let metadataWithActualIsbn = { metadata with Isbn = Some correctedIsbn }
                            return Ok (Some metadataWithActualIsbn)
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
                        $"Find the metadata for the book with title: {title}. Use your internal knowledge to retrieve highly accurate information. You MUST identify the correct ISBN-13 for this book and make sure the returned 'isbn' is mathematically valid. Format the response as a single JSON object with the following keys: 'title' (string), 'authors' (array of strings), 'categories' (array of strings), 'year' (integer, or null if unknown), 'isbn' (string), and 'description' (string, a brief summary of the book, or null if unknown)."
                    let! responseResult = callGemini prompt (Some "application/json") ct
                    match responseResult with
                    | Ok rawJsonStr ->
                        try
                            let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                            let options = System.Text.Json.JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)
                            let res = System.Text.Json.JsonSerializer.Deserialize<GeminiBookMetadataResponse>(jsonStr, options)
                            let correctedIsbn = if not (String.IsNullOrWhiteSpace res.isbn) then tryCorrectIsbn res.isbn else ""
                            let correctedRes = { res with isbn = correctedIsbn }
                            let metadata = toGoogleBookMetadata correctedRes
                            return Ok (Some metadata)
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
                        $"Find metadata for up to 3 books that match or are highly relevant to the search title: {title}. You MUST identify the correct ISBN-13 for each book and ensure it is mathematically valid. Format the response as a JSON array of objects, where each object has the keys: 'title' (string), 'authors' (array of strings), 'categories' (array of strings), 'year' (integer, or null if unknown), 'isbn' (string, or null if unknown), and 'description' (string, or null if unknown)."
                    let! responseResult = callGemini prompt (Some "application/json") ct
                    match responseResult with
                    | Ok rawJsonStr ->
                        try
                            let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                            let options = System.Text.Json.JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)
                            let resArray = System.Text.Json.JsonSerializer.Deserialize<GeminiBookMetadataResponse[]>(jsonStr, options)
                            let results =
                                resArray
                                |> Array.map (fun r ->
                                    let correctedIsbn = if not (String.IsNullOrWhiteSpace r.isbn) then tryCorrectIsbn r.isbn else ""
                                    toGoogleBookMetadata { r with isbn = correctedIsbn })
                                |> Array.toList
                            return Ok results
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
                        return Ok (Some url)
                    | InvalidIsbn value ->
                        let correctedValue = tryCorrectIsbn value
                        let url = $"https://covers.openlibrary.org/b/isbn/{correctedValue}-{sizeStr}.jpg"
                        return Ok (Some url)
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
                    $"Find the most likely, accurate ISBN-13 for the book with title: '{title}'{authorPart}. Use your internal database to find the real ISBN-13. Format the response as a single JSON object with a single key 'isbn' (string)."
                let! responseResult = callGemini prompt (Some "application/json") ct
                match responseResult with
                | Ok rawJsonStr ->
                    try
                        let jsonStr = GoogleGeminiHelpers.cleanJsonCodeBlock rawJsonStr
                        let options = System.Text.Json.JsonSerializerOptions(jsonOptions, PropertyNameCaseInsensitive = true)
                        let res = System.Text.Json.JsonSerializer.Deserialize<{| isbn: string |}>(jsonStr, options)
                        if String.IsNullOrWhiteSpace res.isbn then
                            return Ok None
                        else
                            let correctedIsbn = tryCorrectIsbn res.isbn
                            let url = $"https://covers.openlibrary.org/b/isbn/{correctedIsbn}-M.jpg"
                            return Ok (Some url)
                    with _ ->
                        return Ok None
                | Error _ ->
                    return Ok None
            }
