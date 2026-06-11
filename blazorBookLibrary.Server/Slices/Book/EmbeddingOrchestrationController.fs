namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System.Threading.Tasks
open System

[<ApiController>]
[<Route("api/[controller]")>]
type EmbeddingOrchestrationController(embeddingOrchestrationService: IEmbeddingOrchestrationService) =
    inherit ControllerBase()

    [<HttpPost("create-embedding")>]
    member this.CreateEmbeddingForBook([<FromBody>] bookId: Guid) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let bookIdUnion = BookId bookId
            let! result = embeddingOrchestrationService.CreateEmbeddingForBookAsync(context, bookIdUnion)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("create-embeddings-if-missing")>]
    member this.CreateEmbeddingsForBooksIfMissing([<FromBody>] bookIds: System.Collections.Generic.List<Guid>) =
        task {
            let context = UserContextMapper.mapFromRequest this.Request
            let fsBookIds = bookIds |> Seq.map BookId |> List.ofSeq
            let! result = embeddingOrchestrationService.CreateEmbeddingsForBooksIfMissingAsync(context, fsBookIds)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

