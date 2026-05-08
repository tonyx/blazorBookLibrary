
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System.Threading.Tasks
open System
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type AIAssistantController(textEmbeddingService: ITextEmbeddingService) =
    inherit ControllerBase()

    [<HttpPost("embedding")>]
    member this.GetEmbedding([<FromBody>] text: string) =
        task {
            let! result = textEmbeddingService.GetEmbeddingAsync(text)
            match result with
            | Ok embedding -> return this.Ok(embedding) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("explain-match")>]
    member this.ExplainMatch([<FromBody>] request: {| query: string; itemText: string |}) =
        task {
            let! result = textEmbeddingService.GetMatchExplanationAsync(request.query, request.itemText)
            match result with
            | Ok explanation -> return this.Ok(explanation) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("generate-description")>]
    member this.GenerateDescription([<FromBody>] bookData: PartialBookDataMatch) =
        task {
            let! result = textEmbeddingService.GetBookDescriptionAsync(bookData)
            match result with
            | Ok description -> return this.Ok(description) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("identify-from-cover")>]
    member this.IdentifyFromCover([<FromBody>] request: {| base64Image: string; mimeType: string |}) =
        task {
            let! result = textEmbeddingService.GetPartialBookMatchByCoverImage(request.base64Image, request.mimeType)
            match result with
            | Ok matchResult -> return this.Ok(matchResult) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
