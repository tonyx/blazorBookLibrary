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
open Sharpino.PgStorage
open Sharpino.EventBroker

type IndustryIdentifier =
    { [<JsonPropertyName("type")>]
      Type: string
      [<JsonPropertyName("identifier")>]
      Identifier: string }

type VolumeInfo =
    { [<JsonPropertyName("title")>]
      Title: string
      [<JsonPropertyName("authors")>]
      Authors: string[]
      [<JsonPropertyName("publishedDate")>]
      PublishedDate: string
      [<JsonPropertyName("industryIdentifiers")>]
      IndustryIdentifiers: IndustryIdentifier[]
      [<JsonPropertyName("categories")>]
      Categories: string[]
      [<JsonPropertyName("imageLinks")>]
      ImageLinks: ImageLinks
      [<JsonPropertyName("description")>]
      Description: string }

type GoogleBookItem =
    { [<JsonPropertyName("volumeInfo")>]
      VolumeInfo: VolumeInfo }

type GoogleBooksResponse =
    { [<JsonPropertyName("items")>]
      Items: GoogleBookItem[] }

type GoogleBooksService
    (
        httpClient: HttpClient,
        configuration: IConfiguration,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userTenantResolverService: IUserTenantResolverService
    ) =
    let apiKey = configuration.["GoogleBookApiKey"]

    let secretsReader = SecretsReader(configuration)
    let eventStore = PgEventStore(secretsReader.GetBookLibraryConnectionString())
    let geminiApiKey = configuration.GetValue<string>("GoogleVectorApiKey")
    let geminiModel = GoogleGeminiHelpers.resolveGeminiModel configuration None

    let geminiService =
        GeminiBasedBooksMetadataSearchService(
            eventStore,
            MessageSenders.NoSender,
            secretsReader,
            httpClient,
            tenantViewerAsync,
            userTenantResolverService,
            geminiApiKey,
            geminiModel
        )

    let runWithFallback (primaryCall: unit -> Task<Result<'T, string>>) (backupCall: unit -> Task<Result<'T, string>>) =
        task {
            try
                let! result = primaryCall ()

                match result with
                | Ok res -> return Ok res
                | Error err ->
                    printfn "GoogleBooksService: Primary call failed: %s. Falling back to Gemini." err
                    return! backupCall ()
            with ex ->
                printfn "GoogleBooksService: Primary call threw: %s. Falling back to Gemini." ex.Message
                return! backupCall ()
        }

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    let timeoutMs =
        match Int32.TryParse(configuration.["GoogleBooksApiTimeoutMs"]) with
        | true, v -> v
        | _ -> 10000

    let withTimeout (fn: System.Threading.CancellationToken -> Task<Result<'T, string>>) =
        task {
            use cts = new System.Threading.CancellationTokenSource(timeoutMs)

            try
                return! fn cts.Token
            with
            | :? OperationCanceledException -> return Error "Request timed out"
            | ex -> return Error ex.Message
        }

    let keyParam = if String.IsNullOrWhiteSpace apiKey then "" else $"&key={apiKey}"

    let searchGoogleBooksCover (title: string) (author: string option) (ct: CancellationToken) : Task<string option> =
        task {
            let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
            let authorPart =
                match author with
                | Some a when not (String.IsNullOrWhiteSpace a) ->
                    let encodedAuthor = System.Web.HttpUtility.UrlEncode(a)
                    $"+inauthor:{encodedAuthor}"
                | _ -> ""

            let queries =
                [ $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}{authorPart}{keyParam}"
                  $"https://www.googleapis.com/books/v1/volumes?q=\"{encodedTitle}\"{authorPart}{keyParam}"
                  $"https://www.googleapis.com/books/v1/volumes?q={encodedTitle}{authorPart}{keyParam}" ]

            let rec tryQueries qList =
                task {
                    match qList with
                    | [] -> return None
                    | (queryUrl: string) :: rest ->
                        try
                            use req = new HttpRequestMessage(HttpMethod.Get, queryUrl)
                            req.Headers.TryAddWithoutValidation("User-Agent", "BlazorBookLibrary/1.0 (book-library-app)") |> ignore
                            let! response = httpClient.SendAsync(req, ct)
                            if response.IsSuccessStatusCode then
                                let! respObj = response.Content.ReadFromJsonAsync<GoogleBooksResponse>(cancellationToken = ct)
                                if not (isNull (box respObj)) && not (isNull (box respObj.Items)) && respObj.Items.Length > 0 then
                                    let imgOpt =
                                        respObj.Items
                                        |> Array.tryPick (fun item ->
                                            if not (isNull (box item.VolumeInfo)) && not (isNull (box item.VolumeInfo.ImageLinks)) then
                                                BookCoverHelpers.extractBestGoogleImage item.VolumeInfo.ImageLinks
                                            else
                                                None)
                                    match imgOpt with
                                    | Some url -> return Some url
                                    | None -> return! tryQueries rest
                                else
                                    return! tryQueries rest
                            elif response.StatusCode = System.Net.HttpStatusCode.ServiceUnavailable || response.StatusCode = System.Net.HttpStatusCode.TooManyRequests then
                                return None
                            else
                                return! tryQueries rest
                        with _ ->
                            return! tryQueries rest
                }
            return! tryQueries queries
        }

    let createMetadata (item: VolumeInfo) =
        let isbnOpt =
            if isNull item.IndustryIdentifiers then
                None
            else
                let isbn13 = item.IndustryIdentifiers |> Array.tryFind (fun i -> i.Type = "ISBN_13")
                let isbn10 = item.IndustryIdentifiers |> Array.tryFind (fun i -> i.Type = "ISBN_10")

                match isbn13, isbn10 with
                | Some x, _ -> Some x.Identifier
                | None, Some x -> Some x.Identifier
                | _ -> None

        { Title = item.Title
          Authors =
            if isNull item.Authors then
                List<string>()
            else
                List<string>(item.Authors)
          Categories =
            if isNull item.Categories then
                List<string>()
            else
                List<string>(item.Categories)
          Year =
            match item.PublishedDate with
            | null
            | "" -> None
            | date ->
                let parts = date.Split('-')

                match System.Int32.TryParse(parts.[0]) with
                | (true, year) -> Some year
                | _ -> None
          Isbn = isbnOpt
          Description =
            if String.IsNullOrWhiteSpace(item.Description) then
                None
            else
                Some item.Description }

    new
        (
            httpClient: HttpClient,
            configuration: IConfiguration,
            secretsReader: SecretsReader,
            userTenantResolverService: IUserTenantResolverService
        ) =
        GoogleBooksService(
            httpClient,
            configuration,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> (
                PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString())
            ),
            userTenantResolverService
        )

    interface IBooksMetadataSearchService with
        member this.LookupByIsbnAsync
            (context: UserContext, isbn: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None
            let cleanIsbn = isbn.Replace("-", "").Replace(" ", "").Trim()

            runWithFallback
                (fun () ->
                    withTimeout (fun internalCt ->
                        task {
                            let! allowed = checkIsGlobalAdminOrTenantManager context internalCt

                            if allowed |> Result.isError then
                                return Error "Not authorized to lookup books"
                            else
                                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, internalCt)
                                let url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{cleanIsbn}{keyParam}"
                                let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, linkedCts.Token)

                                if
                                    isNull (box response)
                                    || isNull (box response.Items)
                                    || response.Items.Length = 0
                                then
                                    return! (geminiService :> IBooksMetadataSearchService).LookupByIsbnAsync(context, cleanIsbn, ct)
                                else
                                    let metadata = createMetadata response.Items.[0].VolumeInfo
                                    let metadataWithActualIsbn = { metadata with Isbn = Some cleanIsbn }
                                    return Ok(Some metadataWithActualIsbn)
                        }))
                (fun () -> (geminiService :> IBooksMetadataSearchService).LookupByIsbnAsync(context, cleanIsbn, ct))

        member this.LookupByTitleAsync
            (context: UserContext, title: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None

            runWithFallback
                (fun () ->
                    withTimeout (fun internalCt ->
                        task {
                            let! allowed = checkIsGlobalAdminOrTenantManager context internalCt

                            if allowed |> Result.isError then
                                return Error "Not authorized to lookup books"
                            else
                                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, internalCt)
                                let encodedTitle = System.Web.HttpUtility.UrlEncode(title)

                                let url =
                                    $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}{keyParam}"

                                let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, linkedCts.Token)

                                let! finalResponse =
                                    task {
                                        if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                                            let fallbackUrl = $"https://www.googleapis.com/books/v1/volumes?q=\"{encodedTitle}\"{keyParam}"
                                            return! httpClient.GetFromJsonAsync<GoogleBooksResponse>(fallbackUrl, linkedCts.Token)
                                        else
                                            return response
                                    }

                                if
                                    isNull (box finalResponse)
                                    || isNull (box finalResponse.Items)
                                    || finalResponse.Items.Length = 0
                                then
                                    return! (geminiService :> IBooksMetadataSearchService).LookupByTitleAsync(context, title, ct)
                                else
                                    let metadata = createMetadata finalResponse.Items.[0].VolumeInfo
                                    return Ok(Some metadata)
                        }))
                (fun () -> (geminiService :> IBooksMetadataSearchService).LookupByTitleAsync(context, title, ct))

        member this.LookupMultipleByTitleAsync
            (context: UserContext, title: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None

            runWithFallback
                (fun () ->
                    withTimeout (fun internalCt ->
                        task {
                            let! allowed = checkIsGlobalAdminOrTenantManager context internalCt

                            if allowed |> Result.isError then
                                return Error "Not authorized to lookup books"
                            else
                                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, internalCt)
                                let encodedTitle = System.Web.HttpUtility.UrlEncode(title)

                                let url =
                                    $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}{keyParam}"

                                let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, linkedCts.Token)

                                let! finalResponse =
                                    task {
                                        if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                                            let fallbackUrl = $"https://www.googleapis.com/books/v1/volumes?q=\"{encodedTitle}\"{keyParam}"
                                            return! httpClient.GetFromJsonAsync<GoogleBooksResponse>(fallbackUrl, linkedCts.Token)
                                        else
                                            return response
                                    }

                                if isNull (box finalResponse) || isNull (box finalResponse.Items) || finalResponse.Items.Length = 0 then
                                    return! (geminiService :> IBooksMetadataSearchService).LookupMultipleByTitleAsync(context, title, ct)
                                else
                                    let results =
                                        finalResponse.Items
                                        |> Array.map (fun item -> createMetadata item.VolumeInfo)
                                        |> Array.toList

                                    return Ok results
                        }))
                (fun () ->
                    (geminiService :> IBooksMetadataSearchService).LookupMultipleByTitleAsync(context, title, ct))

        member this.LookupCoverImageByIsbnAsync
            (
                context: UserContext,
                isbn: Isbn,
                [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            let size = defaultArg thumbRoughSize ThumbRoughSize.Medium

            runWithFallback
                (fun () ->
                    withTimeout (fun internalCt ->
                        task {
                            let! allowed = checkIsGlobalAdminOrTenantManager context internalCt

                            if allowed |> Result.isError then
                                return Error "Not authorized to lookup book covers"
                            else
                                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, internalCt)
                                let sizeStr = size.ShortPrint

                                match isbn with
                                | Isbn value ->
                                    let cleanVal = value.Replace("-", "").Replace(" ", "").Trim()
                                    let url = $"https://covers.openlibrary.org/b/isbn/{cleanVal}-{sizeStr}.jpg"
                                    let! direct = BookCoverHelpers.verifyCoverUrl httpClient url linkedCts.Token
                                    match direct with
                                    | Some u -> return Ok (Some u)
                                    | None ->
                                        let! fromEdition = BookCoverHelpers.checkOpenLibraryEditionCovers httpClient cleanVal sizeStr linkedCts.Token
                                        return Ok fromEdition
                                | InvalidIsbn _ -> return Error "Cannot lookup cover for an invalid ISBN."
                                | EmptyIsbn -> return Ok None
                        }))
                (fun () ->
                    (geminiService :> IBooksMetadataSearchService).LookupCoverImageByIsbnAsync(context, isbn, size, ct))

        member this.LookupGoogleApiCoverImageByIsbnAsync
            (context: UserContext, isbn: Isbn, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None

            runWithFallback
                (fun () ->
                    withTimeout (fun internalCt ->
                        task {
                            let! allowed = checkIsGlobalAdminOrTenantManager context internalCt

                            if allowed |> Result.isError then
                                return Error "Not authorized to lookup book covers"
                            else
                                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, internalCt)
                                let isbnStr = isbn.Value.Replace("-", "").Replace(" ", "").Trim()

                                if String.IsNullOrWhiteSpace isbnStr then
                                    return Ok None
                                else
                                    let url =
                                        $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbnStr}{keyParam}"

                                    use req = new HttpRequestMessage(HttpMethod.Get, url)
                                    req.Headers.TryAddWithoutValidation("User-Agent", "BlazorBookLibrary/1.0 (book-library-app)") |> ignore
                                    let! response = httpClient.SendAsync(req, linkedCts.Token)

                                    if response.IsSuccessStatusCode then
                                        let! respObj = response.Content.ReadFromJsonAsync<GoogleBooksResponse>(cancellationToken = linkedCts.Token)
                                        if
                                            isNull (box respObj)
                                            || isNull (box respObj.Items)
                                            || respObj.Items.Length = 0
                                        then
                                            return Ok None
                                        else
                                            let imgOpt =
                                                respObj.Items
                                                |> Array.tryPick (fun item ->
                                                    if not (isNull (box item.VolumeInfo)) && not (isNull (box item.VolumeInfo.ImageLinks)) then
                                                        BookCoverHelpers.extractBestGoogleImage item.VolumeInfo.ImageLinks
                                                    else
                                                        None)
                                            return Ok imgOpt
                                    else
                                        return Ok None
                        }))
                (fun () ->
                    (geminiService :> IBooksMetadataSearchService)
                        .LookupGoogleApiCoverImageByIsbnAsync(context, isbn, ct))

        member this.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync
            (
                context: UserContext,
                isbn: Isbn,
                [<Optional; DefaultParameterValue(null)>] ?thumbRoughSize: ThumbRoughSize,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            let size = defaultArg thumbRoughSize ThumbRoughSize.Medium

            runWithFallback
                (fun () ->
                    task {
                        let! allowed = checkIsGlobalAdminOrTenantManager context ct

                        if allowed |> Result.isError then
                            return Error "Not authorized to lookup book covers"
                        else
                            let! openLibraryResult =
                                (this :> IBooksMetadataSearchService)
                                    .LookupCoverImageByIsbnAsync(context, isbn, size, ct)

                            match openLibraryResult with
                            | Ok(Some url) -> return Ok(Some url)
                            | _ ->
                                let! googleResult =
                                    (this :> IBooksMetadataSearchService)
                                        .LookupGoogleApiCoverImageByIsbnAsync(context, isbn, ct)
                                match googleResult with
                                | Ok(Some url) -> return Ok(Some url)
                                | _ ->
                                    // Fallback: lookup metadata by ISBN and then search cover by title/author
                                    let cleanVal = isbn.Value.Replace("-", "").Replace(" ", "").Trim()
                                    let! metaResult = (this :> IBooksMetadataSearchService).LookupByIsbnAsync(context, cleanVal, ct)
                                    match metaResult with
                                    | Ok(Some meta) when not (String.IsNullOrWhiteSpace meta.Title) ->
                                        let authorOpt =
                                            if not (isNull (box meta.Authors)) && meta.Authors.Count > 0 then
                                                Some meta.Authors.[0]
                                            else
                                                None
                                        return!
                                            (this :> IBooksMetadataSearchService)
                                                .LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(
                                                    context,
                                                    meta.Title,
                                                    ?author = authorOpt,
                                                    ct = ct
                                                )
                                    | _ -> return Ok None
                    })
                (fun () ->
                    (geminiService :> IBooksMetadataSearchService)
                        .LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(context, isbn, size, ct))

        member this.LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync
            (
                context: UserContext,
                title: string,
                [<Optional; DefaultParameterValue(null)>] ?author: string,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            runWithFallback
                (fun () ->
                    withTimeout (fun internalCt ->
                        task {
                            let! allowed = checkIsGlobalAdminOrTenantManager context internalCt
                            if allowed |> Result.isError then
                                return Error "Not authorized to lookup book covers"
                            else
                                use linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, internalCt)

                                if String.IsNullOrWhiteSpace title then
                                    return Ok None
                                else
                                    // 1. Try Google Books (with fallback queries & scanning all items)
                                    let! googleResult = searchGoogleBooksCover title author linkedCts.Token
                                    match googleResult with
                                    | Some url -> return Ok (Some url)
                                    | None ->
                                        // 2. Try OpenLibrary Search API (by cover_i and ISBN)
                                        let! openLibResult = BookCoverHelpers.searchOpenLibraryCover httpClient title author "M" linkedCts.Token
                                        match openLibResult with
                                        | Some url -> return Ok (Some url)
                                        | None -> return Ok None
                        }))
                (fun () ->
                    (geminiService :> IBooksMetadataSearchService)
                        .LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(
                            context,
                            title,
                            ?author = author,
                            ct = ct
                        ))
