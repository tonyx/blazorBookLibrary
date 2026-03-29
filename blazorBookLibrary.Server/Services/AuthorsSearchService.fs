namespace BookLibrary.Services

open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open BookLibrary.Shared.Services
open System.Text.Json.Serialization
open Microsoft.FSharp.Core
open FsToolkit.ErrorHandling

type OpenLibraryAuthorSearchDoc = {
    [<JsonPropertyName("key")>] Key: string
    [<JsonPropertyName("name")>] Name: string
}

type OpenLibraryAuthorSearchResponse = {
    [<JsonPropertyName("numFound")>] NumFound: int
    [<JsonPropertyName("docs")>] Docs: OpenLibraryAuthorSearchDoc[]
}

type OpenLibraryRemoteIds = {
    [<JsonPropertyName("isni")>] Isni: string
}

type OpenLibraryAuthorDetails = {
    [<JsonPropertyName("remote_ids")>] RemoteIds: OpenLibraryRemoteIds
}

type AuthorsSearchService(httpClient: HttpClient) =
    interface IAuthorsSearchService with
        member this.LookupByNameAsync(name: string) =
            task {
                try
                    // URL encode the name
                    let encodedName = System.Web.HttpUtility.UrlEncode(name)
                    let url = $"https://openlibrary.org/search/authors.json?q={encodedName}"
                    let! response = httpClient.GetFromJsonAsync<OpenLibraryAuthorSearchResponse>(url)
                    
                    if isNull (box response) || isNull (box response.Docs) || response.Docs.Length = 0 then
                        return Error "Author not found"
                    else
                        let doc = response.Docs.[0]
                        let authorKey = doc.Key

                        let mutable isniOpt = None

                        try
                            // Optional secondary call to fetch remote ids (like ISNI) if present
                            let detailsUrl = $"https://openlibrary.org/authors/{authorKey}.json"
                            let! details = httpClient.GetFromJsonAsync<OpenLibraryAuthorDetails>(detailsUrl)
                            if not (isNull (box details)) && not (isNull (box details.RemoteIds)) && not (System.String.IsNullOrWhiteSpace(details.RemoteIds.Isni)) then
                                isniOpt <- Some details.RemoteIds.Isni
                        with
                        | _ -> () // ignore if details can't be fetched or parsing fails

                        return Ok { Name = doc.Name; Isni = isniOpt }
                with
                | ex -> return Error ex.Message
            }

        member this.LookupImageUrlByNameAndThumbSizeAsync(name: string, ?pitThumbSize: int) =
            task {
                let thumbSize = defaultArg pitThumbSize 100
                try
                    // URL encode the name
                    let encodedName = System.Web.HttpUtility.UrlEncode(name)
                    // Using Italian Wikipedia as per the example provided
                    let url = $"https://it.wikipedia.org/w/api.php?action=query&titles={encodedName}&prop=pageimages&format=json&pithumbsize={thumbSize}"
                    
                    let! jsonDoc = httpClient.GetFromJsonAsync<System.Text.Json.JsonDocument>(url)
                    
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
                                    | true, sourceElement ->
                                        return Ok (sourceElement.GetString())
                                    | false, _ ->
                                        return Error "Source property not found in thumbnail"
                                | false, _ ->
                                    return Error "Thumbnail property not found in page"
                            | None ->
                                return Error "No pages found in Wikipedia response"
                with
                | ex -> return Error ex.Message
            }
