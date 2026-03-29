namespace BookLibrary.Services

open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks
open Microsoft.Extensions.Configuration
open BookLibrary.Shared.Services
open System.Text.Json.Serialization
open System.Collections.Generic

type IndustryIdentifier = {
    [<JsonPropertyName("type")>] Type: string
    [<JsonPropertyName("identifier")>] Identifier: string
}

type VolumeInfo = {
    [<JsonPropertyName("title")>] Title: string
    [<JsonPropertyName("authors")>] Authors: string[]
    [<JsonPropertyName("publishedDate")>] PublishedDate: string
    [<JsonPropertyName("industryIdentifiers")>] IndustryIdentifiers: IndustryIdentifier[]
    [<JsonPropertyName("categories")>] Categories: string[]
}

type GoogleBookItem = {
    [<JsonPropertyName("volumeInfo")>] VolumeInfo: VolumeInfo
}

type GoogleBooksResponse = {
    [<JsonPropertyName("items")>] Items: GoogleBookItem[]
}

type GoogleBooksService(httpClient: HttpClient, configuration: IConfiguration) =
    let apiKey = configuration.["GoogleBookApiKey"]

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
        }

    interface IGoogleBooksService with
        member this.LookupByIsbnAsync(isbn: string) =
            task {
                try
                    let url = $"https://www.googleapis.com/books/v1/volumes?q=isbn:{isbn}&key={apiKey}"
                    let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url)
                    
                    if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                        return Ok None
                    else
                        let metadata = createMetadata response.Items.[0].VolumeInfo
                        let metadataWithActualIsbn = { metadata with Isbn = Some isbn }
                        return Ok (Some metadataWithActualIsbn)
                with
                | ex -> return Error ex.Message
            }

        member this.LookupByTitleAsync(title: string) =
            task {
                try
                    let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
                    let url = $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}&key={apiKey}"
                    let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url)
                    
                    if isNull (box response) || isNull (box response.Items) || response.Items.Length = 0 then
                        return Ok None
                    else
                        let metadata = createMetadata response.Items.[0].VolumeInfo
                        return Ok (Some metadata)
                with
                | ex -> return Error ex.Message
            }

        member this.LookupMultipleByTitleAsync(title: string) =
            task {
                try
                    let encodedTitle = System.Web.HttpUtility.UrlEncode(title)
                    let url = $"https://www.googleapis.com/books/v1/volumes?q=intitle:{encodedTitle}&key={apiKey}"
                    let! response = httpClient.GetFromJsonAsync<GoogleBooksResponse>(url)
                    
                    if isNull (box response) || isNull (box response.Items) then
                        return Ok []
                    else
                        let results = 
                            response.Items 
                            |> Array.map (fun item -> createMetadata item.VolumeInfo)
                            |> Array.toList
                        return Ok results
                with
                | ex -> return Error ex.Message
            }
