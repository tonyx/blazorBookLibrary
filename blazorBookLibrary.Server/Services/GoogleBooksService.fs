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

type IndustryIdentifier = {
    [<JsonPropertyName("type")>] Type: string
    [<JsonPropertyName("identifier")>] Identifier: string
}

type ImageLinks = {
    [<JsonPropertyName("smallThumbnail")>] SmallThumbnail: string
    [<JsonPropertyName("thumbnail")>] Thumbnail: string
    [<JsonPropertyName("small")>] Small: string
    [<JsonPropertyName("medium")>] Medium: string
    [<JsonPropertyName("large")>] Large: string
    [<JsonPropertyName("extraLarge")>] ExtraLarge: string
}

type VolumeInfo = {
    [<JsonPropertyName("title")>] Title: string
    [<JsonPropertyName("authors")>] Authors: string[]
    [<JsonPropertyName("publishedDate")>] PublishedDate: string
    [<JsonPropertyName("industryIdentifiers")>] IndustryIdentifiers: IndustryIdentifier[]
    [<JsonPropertyName("categories")>] Categories: string[]
    [<JsonPropertyName("imageLinks")>] ImageLinks: ImageLinks
    [<JsonPropertyName("description")>] Description: string
}

type GoogleBookItem = {
    [<JsonPropertyName("volumeInfo")>] VolumeInfo: VolumeInfo
}

type GoogleBooksResponse = {
    [<JsonPropertyName("items")>] Items: GoogleBookItem[]
}

type GoogleBooksService(httpClient: HttpClient, configuration: IConfiguration) =
    let apiKey = configuration.["GoogleBookApiKey"]
    let timeoutMs = 
        match Int32.TryParse(configuration.["GoogleBooksApiTimeoutMs"]) with
        | true, v -> v
        | _ -> 5000

    let withTimeout (fn: System.Threading.CancellationToken -> Task<Result<'T, string>>) =
        task {
            use cts = new System.Threading.CancellationTokenSource(timeoutMs)
            try
                return! fn cts.Token
            with
            | :? OperationCanceledException -> return Error "Request timed out"
            | ex -> return Error ex.Message
        }

    let createMetadata (item: VolumeInfo) =
        let isbnOpt = 
            if isNull item.IndustryIdentifiers then None
            else
                let isbn13 = item.IndustryIdentifiers |> Array.tryFind (fun i -> i.Type = "ISBN_13")
                let isbn10 = item.IndustryIdentifiers |> Array.tryFind (fun i -> i.Type = "ISBN_10")
                match isbn13, isbn10 with
                | Some x, _ -> Some x.Identifier
                | None, Some x -> Some x.Identifier
                | _ -> None
                
        {
            Title = item.Title
            Authors = if isNull item.Authors then List<string>() else List<string>(item.Authors)
            Categories = if isNull item.Categories then List<string>() else List<string>(item.Categories)
            Year = 
                match item.PublishedDate with
                | null | "" -> None
                | date -> 
                    let parts = date.Split('-')
                    match System.Int32.TryParse(parts.[0]) with
                    | (true, year) -> Some year
                    | _ -> None
            Isbn = isbnOpt
            Description = if String.IsNullOrWhiteSpace(item.Description) then None else Some item.Description
        }

    interface IGoogleBooksService with
        member this.LookupByIsbnAsync(isbn: string) =
            withTimeout (fun ct -> task {
                let url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}&key={apiKey}"
                let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct)
                
                if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                    return Ok None
                else
                    let metadata = createMetadata response.Items.[0].VolumeInfo
                    let metadataWithActualIsbn = { metadata with Isbn = Some isbn }
                    return Ok (Some metadataWithActualIsbn)
            })

        member this.LookupByTitleAsync(title: string) =
            withTimeout (fun ct -> task {
                let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
                let url = $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}&key={apiKey}"
                let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct)
                
                if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                    return Ok None
                else
                    let metadata = createMetadata response.Items.[0].VolumeInfo
                    return Ok (Some metadata)
            })

        member this.LookupMultipleByTitleAsync(title: string) =
            withTimeout (fun ct -> task {
                let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
                let url = $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}&key={apiKey}"
                let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct)
                
                if isNull (box response) || isNull (box response.Items) then
                    return Ok []
                else
                    let results = 
                        response.Items 
                        |> Array.map (fun item -> createMetadata item.VolumeInfo)
                        |> Array.toList
                    return Ok results
            })

        member this.LookupCoverImageByIsbnAsync(isbn: Isbn, ?thumbRoughSize: ThumbRoughSize) =
            withTimeout (fun ct -> task {
                let size = defaultArg thumbRoughSize ThumbRoughSize.Medium
                let sizeStr = size.ShortPrint
                
                match isbn with
                | Isbn value ->
                    let url = $"https://covers.openlibrary.org/b/isbn/{value}-{sizeStr}.jpg"
                    let! response = httpClient.GetAsync(url, ct)
                    if response.IsSuccessStatusCode then
                        let finalUrl = response.RequestMessage.RequestUri.ToString()
                        let! content = response.Content.ReadAsByteArrayAsync(ct)
                        if content.Length > 1000 && not (finalUrl.Contains("blank")) && finalUrl <> url then
                            return Ok (Some finalUrl)
                        else
                            return Ok None
                    else
                        return Ok None
                | InvalidIsbn _ ->
                    return Error "Cannot lookup cover for an invalid ISBN."
                | EmptyIsbn ->
                    return Ok None
            })

        member this.LookupGoogleApiCoverImageByIsbnAsync(isbn: Isbn) =
            withTimeout (fun ct -> task {
                let isbnStr = isbn.Value
                if String.IsNullOrWhiteSpace isbnStr then return Ok None
                else
                    let url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbnStr}&key={apiKey}"
                    let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct)
                    
                    if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                        return Ok None
                    else
                        let firstItem = response.Items.[0]
                        if not (isNull (box firstItem.VolumeInfo.ImageLinks)) && not (String.IsNullOrWhiteSpace firstItem.VolumeInfo.ImageLinks.Thumbnail) then
                            return Ok (Some firstItem.VolumeInfo.ImageLinks.Thumbnail)
                        else
                            return Ok None
            })

        member this.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(isbn: Isbn, ?thumbRoughSize: ThumbRoughSize) =
            task {
                let size = defaultArg thumbRoughSize ThumbRoughSize.Medium
                let! openLibraryResult = (this :> IGoogleBooksService).LookupCoverImageByIsbnAsync(isbn, size)
                
                match openLibraryResult with
                | Ok (Some url) -> return Ok (Some url)
                | _ -> 
                    // If Open Library fails or returns None, try Google API
                    return! (this :> IGoogleBooksService).LookupGoogleApiCoverImageByIsbnAsync(isbn)
            }

        member this.LookupGoogleApiCoverImageByTitleAndOptionalAuthorAsync(title: string, ?author: string) =
            withTimeout (fun ct -> task {
                if String.IsNullOrWhiteSpace title then return Ok None
                else
                    let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
                    let authorPart = 
                        match author with
                        | Some a when not (String.IsNullOrWhiteSpace a) -> 
                            let encodedAuthor = System.Web.HttpUtility.UrlEncode(a)
                            $"+inauthor:{encodedAuthor}"
                        | _ -> ""
                    
                    let url = $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}{authorPart}&key={apiKey}"
                    let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url, ct)
                    
                    if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                        return Ok None
                    else
                        let firstItem = response.Items.[0]
                        if not (isNull (box firstItem.VolumeInfo.ImageLinks)) && not (String.IsNullOrWhiteSpace firstItem.VolumeInfo.ImageLinks.Thumbnail) then
                            return Ok (Some firstItem.VolumeInfo.ImageLinks.Thumbnail)
                        else
                            return Ok None
            })
