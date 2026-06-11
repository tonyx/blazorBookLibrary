namespace BookLibrary.Services

open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open BookLibrary.Shared.Services
open System.Text.Json.Serialization
open Microsoft.FSharp.Core
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons
open System.Threading
open System.Runtime.InteropServices
open Sharpino
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Utils
open Sharpino.CommandHandler
open Microsoft.Extensions.DependencyInjection


type OpenLibraryAuthorSearchDoc =
    { [<JsonPropertyName("key")>]
      Key: string
      [<JsonPropertyName("name")>]
      Name: string }

type OpenLibraryAuthorSearchResponse =
    { [<JsonPropertyName("numFound")>]
      NumFound: int
      [<JsonPropertyName("docs")>]
      Docs: OpenLibraryAuthorSearchDoc[] }

type OpenLibraryRemoteIds =
    { [<JsonPropertyName("isni")>]
      Isni: string }

type OpenLibraryAuthorDetails =
    { [<JsonPropertyName("remote_ids")>]
      RemoteIds: OpenLibraryRemoteIds }


type AuthorsSearchService
    (


        httpClient: HttpClient,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userTenantResolverService: IUserTenantResolverService
    ) =

    let checkIsGlobalAdminOrTenantManager (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (ct |> Some) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }



    [<ActivatorUtilitiesConstructor>]
    new(httpClient: HttpClient, secretsReader: SecretsReader, userTenantResolverService: IUserTenantResolverService) =

        AuthorsSearchService(
            httpClient,
            getAggregateStorageFreshStateViewerAsync<Tenant, TenantEvent, string> (
                PgStorage.PgEventStore(secretsReader.GetBookLibraryConnectionString())
            ),
            userTenantResolverService
        )

    interface IAuthorsSearchService with
        member this.LookupByNameAsync
            (context: UserContext, name: string, [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken)
            =
            let ct = defaultArg ct CancellationToken.None

            task {
                try
                    let! allowed = checkIsGlobalAdminOrTenantManager context ct

                    if allowed |> Result.isError then
                        return Error "Only admin or managers can look up authors"
                    else
                        // URL encode the name
                        let encodedName = System.Web.HttpUtility.UrlEncode(name)
                        let url = $"https://openlibrary.org/search/authors.json?q={encodedName}"
                        let! response = httpClient.GetFromJsonAsync<OpenLibraryAuthorSearchResponse>(url, ct)

                        if isNull (box response) || isNull (box response.Docs) || response.Docs.Length = 0 then
                            return Error "Author not found"
                        else
                            let doc = response.Docs.[0]
                            let authorKey = doc.Key

                            let mutable isniOpt = None

                            try
                                // Optional secondary call to fetch remote ids (like ISNI) if present
                                let detailsUrl = $"https://openlibrary.org/authors/{authorKey}.json"
                                let! details = httpClient.GetFromJsonAsync<OpenLibraryAuthorDetails>(detailsUrl, ct)

                                if
                                    not (isNull (box details))
                                    && not (isNull (box details.RemoteIds))
                                    && not (System.String.IsNullOrWhiteSpace(details.RemoteIds.Isni))
                                then
                                    isniOpt <- Some details.RemoteIds.Isni
                            with _ ->
                                () // ignore if details can't be fetched or parsing fails

                            return Ok { Name = doc.Name; Isni = isniOpt }
                with ex ->
                    return Error ex.Message
            }

        member this.LookupImageUrlByNameAndThumbSizeAsync
            (
                context: UserContext,
                name: string,
                [<Optional; DefaultParameterValue(null)>] ?pitThumbSize: int,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None

            task {
                let thumbSize = defaultArg pitThumbSize 100

                try
                    // URL encode the name
                    let encodedName = System.Web.HttpUtility.UrlEncode(name)
                    // Using Italian Wikipedia as per the example provided
                    let url =
                        $"https://it.wikipedia.org/w/api.php?action=query&titles={encodedName}&prop=pageimages&format=json&pithumbsize={thumbSize}"

                    let! jsonDoc = httpClient.GetFromJsonAsync<System.Text.Json.JsonDocument>(url, ct)

                    let root = jsonDoc.RootElement

                    match root.TryGetProperty("query") with
                    | false, _ -> return Error "Query property not found"
                    | true, queryElement ->
                        match queryElement.TryGetProperty("pages") with
                        | false, _ -> return Error "Pages property not found"
                        | true, pagesElement ->
                            // Get the first property of pages
                            let firstPage = pagesElement.EnumerateObject() |> Seq.tryHead

                            match firstPage with
                            | Some page ->
                                let pageValue = page.Value

                                match pageValue.TryGetProperty("thumbnail") with
                                | true, thumbnailElement ->
                                    match thumbnailElement.TryGetProperty("source") with
                                    | true, sourceElement -> return Ok(sourceElement.GetString())
                                    | false, _ -> return Error "Source property not found in thumbnail"
                                | false, _ -> return Error "Thumbnail property not found in page"
                            | None -> return Error "No pages found in Wikipedia response"
                with ex ->
                    return Error ex.Message
            }

        member this.LookupBioByNameAsync
            (
                context: UserContext,
                name: string,
                [<Optional; DefaultParameterValue(null)>] ?lang: ShortLang,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            let lang = defaultArg lang (ShortLang.New "it")

            task {
                try
                    let! allowed = checkIsGlobalAdminOrTenantManager context ct

                    if allowed |> Result.isError then
                        return Error "Only admin or managers can look up authors"
                    else
                        let encodedName = System.Web.HttpUtility.UrlEncode(name)
                        let locale = lang.Value
                        // Action query with generator search allows us to retrieve multiple candidates in a single call.
                        let url =
                            $"https://{locale}.wikipedia.org/w/api.php?action=query&format=json&generator=search&gsrsearch={encodedName}&gsrlimit=5&prop=extracts&exintro&explaintext&exlimit=5"

                        let! jsonDoc = httpClient.GetFromJsonAsync<System.Text.Json.JsonDocument>(url, ct)

                        let root = jsonDoc.RootElement

                        match root.TryGetProperty("query") with
                        | false, _ -> return Ok []
                        | true, queryElement ->
                            match queryElement.TryGetProperty("pages") with
                            | false, _ -> return Ok []
                            | true, pagesElement ->
                                let bios =
                                    pagesElement.EnumerateObject()
                                    |> Seq.choose (fun page ->
                                        match page.Value.TryGetProperty("extract") with
                                        | true, extract ->
                                            let bio = extract.GetString()

                                            if System.String.IsNullOrWhiteSpace(bio) then
                                                None
                                            else
                                                Some bio
                                        | false, _ -> None)
                                    |> List.ofSeq

                                return Ok bios
                with ex ->
                    return Error ex.Message
            }

        member this.LookupWikipediaUriByNameAsync
            (
                context: UserContext,
                name: string,
                [<Optional; DefaultParameterValue(null)>] ?lang: ShortLang,
                [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
            ) =
            let ct = defaultArg ct CancellationToken.None
            let lang = defaultArg lang (ShortLang.New "it")

            task {
                try
                    let encodedName = System.Web.HttpUtility.UrlEncode(name)
                    let locale = lang.Value
                    // Use generator search to find the most relevant page and prop=info to get the full URL
                    let url =
                        $"https://{locale}.wikipedia.org/w/api.php?action=query&format=json&generator=search&gsrsearch={encodedName}&gsrlimit=1&prop=info&inprop=url"

                    let! jsonDoc = httpClient.GetFromJsonAsync<System.Text.Json.JsonDocument>(url, ct)

                    let root = jsonDoc.RootElement

                    match root.TryGetProperty("query") with
                    | false, _ -> return Error "Wikipedia page not found"
                    | true, queryElement ->
                        match queryElement.TryGetProperty("pages") with
                        | false, _ -> return Error "Wikipedia page not found"
                        | true, pagesElement ->
                            let firstPage = pagesElement.EnumerateObject() |> Seq.tryHead

                            match firstPage with
                            | Some page ->
                                let pageValue = page.Value

                                match pageValue.TryGetProperty("fullurl") with
                                | true, urlElement -> return Ok(urlElement.GetString())
                                | false, _ -> return Error "Full URL not found for the page"
                            | None -> return Error "Wikipedia page not found"
                with ex ->
                    return Error ex.Message
            }
