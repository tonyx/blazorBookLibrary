
namespace BookLibrary.Services
open System.Threading
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

[<CLIMutable>]
type GoogleEmbeddingValues = { values: float32[] }

[<CLIMutable>]
type GoogleEmbeddingResponse = { embedding: GoogleEmbeddingValues }

[<CLIMutable>]
type GooglePart = { text: string }

[<CLIMutable>]
type GoogleContent = { parts: GooglePart[] }

[<CLIMutable>]
type GoogleCandidate = { content: GoogleContent }

[<CLIMutable>]
type GoogleGenerateResponse = { candidates: GoogleCandidate[] }

type TextEmbeddingService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        httpClient: HttpClient,
        reviewViewerAsync: AggregateViewerAsync2<Review>,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        detailsService: IDetailsService,
        apiKey: string
    ) =
    new (eventStore: IEventStore<string>, httpClient: HttpClient, detailsService: IDetailsService, apiKey: string) =
        let messageSenders = MessageSenders.NoSender
        let reviewViewerAsync = getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        TextEmbeddingService(eventStore, messageSenders, httpClient, reviewViewerAsync, bookViewerAsync, detailsService, apiKey)

    new (configuration: IConfiguration, httpClient: HttpClient, detailsService: IDetailsService, secretsReader: SecretsReader) = 
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let apiKey = configuration.GetValue<string>("GoogleVectorApiKey")
        if String.IsNullOrWhiteSpace apiKey then
            failwith "GoogleVectorApiKey is missing in configuration"
        let eventStore = PgStorage.PgEventStore connectionString
        TextEmbeddingService(eventStore, httpClient, detailsService, apiKey)

    interface ITextEmbeddingService with
        member this.GetEmbeddingAsync(text: string) =
            task {
                try
                    let modelName = "models/gemini-embedding-2"
                    let url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-embedding-2:embedContent?key={apiKey}"
                    
                    let requestBody = {| 
                        model = modelName
                        content = {| parts = [| {| text = text |} |] |}
                        output_dimensionality = 1536
                    |}
                    
                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                    
                    let! response = httpClient.PostAsync(url, content)
                    
                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync()
                        return Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync()
                        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
                        let result = JsonSerializer.Deserialize<GoogleEmbeddingResponse>(jsonResponse, options)
                        return Ok { Model = modelName; Vector = result.embedding.values }
                with
                | ex -> return Error ex.Message
            }

        member this.GetMatchExplanationAsync(query: string) (itemText: string) =
            task {
                try
                    let modelName = "gemini-2.5-flash-lite"
                    let url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelName}:generateContent?key={apiKey}"
                    
                    let prompt = $"Explain concisely why the following query matches the provided text. Use the same language as the query.\n\nQuery: {query}\n\nText: {itemText}\n\nExplanation:"
                    
                    let requestBody = {| 
                        contents = [| 
                            {| parts = [| {| text = prompt |} |] |} 
                        |] 
                    |}
                    
                    let jsonRequest = JsonSerializer.Serialize(requestBody)
                    use content = new StringContent(jsonRequest, Encoding.UTF8, "application/json")
                    
                    let! response = httpClient.PostAsync(url, content)
                    
                    if not response.IsSuccessStatusCode then
                        let! errorMsg = response.Content.ReadAsStringAsync()
                        return Error $"Google API error: {response.StatusCode} - {errorMsg}"
                    else
                        let! jsonResponse = response.Content.ReadAsStringAsync()
                        let options = JsonSerializerOptions(PropertyNameCaseInsensitive = true)
                        let result = JsonSerializer.Deserialize<GoogleGenerateResponse>(jsonResponse, options)
                        
                        if Object.ReferenceEquals(result, null) then
                            return Error "Failed to deserialize Gemini response."
                        elif not (isNull result.candidates) && result.candidates.Length > 0 && 
                             not (Object.ReferenceEquals(result.candidates.[0].content, null)) && 
                             not (Object.ReferenceEquals(result.candidates.[0].content.parts, null)) && 
                             result.candidates.[0].content.parts.Length > 0 then
                            return Ok result.candidates.[0].content.parts.[0].text
                        else
                            return Error "No explanation generated by the model."
                with
                | ex -> return Error ex.Message
            }

        
