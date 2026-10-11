namespace BookLibrary.Services

open System
open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open System.Threading
open System.Text.Json
open System.Text.Json.Serialization

type ImageLinks =
    { [<JsonPropertyName("smallThumbnail")>]
      SmallThumbnail: string
      [<JsonPropertyName("thumbnail")>]
      Thumbnail: string
      [<JsonPropertyName("small")>]
      Small: string
      [<JsonPropertyName("medium")>]
      Medium: string
      [<JsonPropertyName("large")>]
      Large: string
      [<JsonPropertyName("extraLarge")>]
      ExtraLarge: string }

module BookCoverHelpers =
    let ensureHttps (url: string) =
        if String.IsNullOrWhiteSpace url then url
        elif url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) then
            "https://" + url.Substring(7)
        else url

    let extractBestGoogleImage (links: ImageLinks) : string option =
        if isNull (box links) then None
        elif not (String.IsNullOrWhiteSpace links.ExtraLarge) then Some links.ExtraLarge
        elif not (String.IsNullOrWhiteSpace links.Large) then Some links.Large
        elif not (String.IsNullOrWhiteSpace links.Medium) then Some links.Medium
        elif not (String.IsNullOrWhiteSpace links.Small) then Some links.Small
        elif not (String.IsNullOrWhiteSpace links.Thumbnail) then Some links.Thumbnail
        elif not (String.IsNullOrWhiteSpace links.SmallThumbnail) then Some links.SmallThumbnail
        else None
        |> Option.map ensureHttps

    let verifyCoverUrl (httpClient: HttpClient) (url: string) (ct: CancellationToken) : Task<string option> =
        task {
            try
                let checkUrl =
                    if url.Contains("covers.openlibrary.org") && not (url.Contains("default=")) then
                        if url.Contains("?") then $"{url}&default=false" else $"{url}?default=false"
                    else
                        url
                use req = new HttpRequestMessage(HttpMethod.Get, checkUrl)
                req.Headers.TryAddWithoutValidation("User-Agent", "BlazorBookLibrary/1.0 (book-library-app)") |> ignore
                let! response = httpClient.SendAsync(req, ct)
                if response.IsSuccessStatusCode then
                    let finalUrl = response.RequestMessage.RequestUri.ToString()
                    let! content = response.Content.ReadAsByteArrayAsync(ct)
                    if content.Length > 1000 && not (finalUrl.Contains("blank", StringComparison.OrdinalIgnoreCase)) then
                        return Some finalUrl
                    else
                        return None
                else
                    return None
            with _ ->
                return None
        }

    let checkOpenLibraryEditionCovers (httpClient: HttpClient) (isbn: string) (sizeStr: string) (ct: CancellationToken) : Task<string option> =
        task {
            try
                let editionUrl = $"https://openlibrary.org/isbn/{isbn}.json"
                use req = new HttpRequestMessage(HttpMethod.Get, editionUrl)
                req.Headers.TryAddWithoutValidation("User-Agent", "BlazorBookLibrary/1.0 (book-library-app)") |> ignore
                let! resp = httpClient.SendAsync(req, ct)
                if resp.IsSuccessStatusCode then
                    let! jsonStream = resp.Content.ReadAsStreamAsync(ct)
                    use doc = JsonDocument.Parse(jsonStream)
                    let root = doc.RootElement
                    let mutable coversElem = Unchecked.defaultof<JsonElement>
                    if root.TryGetProperty("covers", &coversElem) && coversElem.ValueKind = JsonValueKind.Array then
                        let coverIds =
                            [ for c in coversElem.EnumerateArray() do
                                if c.ValueKind = JsonValueKind.Number then
                                    yield c.GetInt64() ]
                        let rec tryCovers list =
                            task {
                                match list with
                                | [] -> return None
                                | (cid: int64) :: rest ->
                                    if cid > 0L then
                                        let coverUrl = $"https://covers.openlibrary.org/b/id/{cid}-{sizeStr}.jpg"
                                        let! verified = verifyCoverUrl httpClient coverUrl ct
                                        match verified with
                                        | Some v -> return Some v
                                        | None -> return! tryCovers rest
                                    else
                                        return! tryCovers rest
                            }
                        return! tryCovers coverIds
                    else
                        return None
                else
                    return None
            with _ ->
                return None
        }

    let searchOpenLibraryCover (httpClient: HttpClient) (title: string) (author: string option) (sizeStr: string) (ct: CancellationToken) : Task<string option> =
        task {
            try
                let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
                let authorPart =
                    match author with
                    | Some a when not (String.IsNullOrWhiteSpace a) ->
                        let encodedAuthor = System.Web.HttpUtility.UrlEncode(a)
                        $"&author={encodedAuthor}"
                    | _ -> ""
                let searchUrl = $"https://openlibrary.org/search.json?title={encodedTitle}{authorPart}&limit=10"
                use req = new HttpRequestMessage(HttpMethod.Get, searchUrl)
                req.Headers.TryAddWithoutValidation("User-Agent", "BlazorBookLibrary/1.0 (book-library-app)") |> ignore
                let! resp = httpClient.SendAsync(req, ct)
                if resp.IsSuccessStatusCode then
                    let! jsonStream = resp.Content.ReadAsStreamAsync(ct)
                    use doc = JsonDocument.Parse(jsonStream)
                    let root = doc.RootElement
                    let mutable docsElem = Unchecked.defaultof<JsonElement>
                    if root.TryGetProperty("docs", &docsElem) && docsElem.ValueKind = JsonValueKind.Array then
                        let docsList = [ for d in docsElem.EnumerateArray() -> d ]

                        // 1. Try cover_i on each doc
                        let rec tryCoverId docs =
                            task {
                                match docs with
                                | [] -> return None
                                | (docElem: JsonElement) :: rest ->
                                    let mutable coverElem = Unchecked.defaultof<JsonElement>
                                    if docElem.TryGetProperty("cover_i", &coverElem) && coverElem.ValueKind = JsonValueKind.Number then
                                        let coverId = coverElem.GetInt64()
                                        if coverId > 0L then
                                            let coverUrl = $"https://covers.openlibrary.org/b/id/{coverId}-{sizeStr}.jpg"
                                            let! verified = verifyCoverUrl httpClient coverUrl ct
                                            match verified with
                                            | Some v -> return Some v
                                            | None -> return! tryCoverId rest
                                        else
                                            return! tryCoverId rest
                                    else
                                        return! tryCoverId rest
                            }
                        let! byCoverId = tryCoverId docsList
                        match byCoverId with
                        | Some v -> return Some v
                        | None ->
                            // 2. Try ISBNs from docs
                            let rec tryIsbn docs =
                                task {
                                    match docs with
                                    | [] -> return None
                                    | (docElem: JsonElement) :: rest ->
                                        let mutable isbnElem = Unchecked.defaultof<JsonElement>
                                        if docElem.TryGetProperty("isbn", &isbnElem) && isbnElem.ValueKind = JsonValueKind.Array then
                                            let isbns =
                                                [ for i in isbnElem.EnumerateArray() do
                                                    if i.ValueKind = JsonValueKind.String then
                                                        yield i.GetString() ]
                                                |> List.truncate 3
                                            let rec checkIsbnList list =
                                                task {
                                                    match list with
                                                    | [] -> return None
                                                    | (rawIsbn: string) :: isbnsRest ->
                                                        if not (String.IsNullOrWhiteSpace rawIsbn) then
                                                            let coverUrl = $"https://covers.openlibrary.org/b/isbn/{rawIsbn.Trim()}-{sizeStr}.jpg"
                                                            let! verified = verifyCoverUrl httpClient coverUrl ct
                                                            match verified with
                                                            | Some v -> return Some v
                                                            | None -> return! checkIsbnList isbnsRest
                                                        else
                                                            return! checkIsbnList isbnsRest
                                                }
                                            let! byIsbn = checkIsbnList isbns
                                            match byIsbn with
                                            | Some v -> return Some v
                                            | None -> return! tryIsbn rest
                                        else
                                            return! tryIsbn rest
                                }
                            return! tryIsbn docsList
                    else
                        return None
                else
                    return None
            with _ ->
                return None
        }
