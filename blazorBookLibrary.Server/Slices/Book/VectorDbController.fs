namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System.Threading.Tasks
open System
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
[<Produces("application/json")>]
type VectorDbController(vectorDbService: IVectorDbService) =
    inherit ControllerBase()

    [<HttpPost("store")>]
    member this.StoreEmbedding([<FromBody>] request: {| id: Guid; tenantId: Guid; bookId: Guid; model: string; vector: float32[] |}) =
        task {
            let embedding = { Model = request.model; Vector = request.vector }
            let! result = vectorDbService.StoreEmbeddingAsync(EmbeddingDataId request.id, TenantId request.tenantId, BookId request.bookId, embedding)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("read/{id}")>]
    member this.ReadEmbedding([<FromRoute>] id: Guid) =
        task {
            let! result = vectorDbService.ReadEmbeddingAsync(EmbeddingDataId id)
            match result with
            | Ok (data, BookId bookIdGuid) -> 
                return this.Ok({| model = data.Model; vector = data.Vector; bookId = bookIdGuid |}) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("update")>]
    member this.UpdateEmbedding([<FromBody>] request: {| id: Guid; model: string; vector: float32[] |}) =
        task {
            let embedding = { Model = request.model; Vector = request.vector }
            let! result = vectorDbService.UpdateEmbeddingAsync(EmbeddingDataId request.id, embedding)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpDelete("remove/{id}")>]
    member this.RemoveEmbedding([<FromRoute>] id: Guid) =
        task {
            let! result = vectorDbService.RemoveEmbeddingAsync(EmbeddingDataId id)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("remove-multiple")>]
    member this.RemoveEmbeddings([<FromBody>] ids: seq<Guid>) =
        task {
            let! result = vectorDbService.RemoveEmbeddingsAsync(ids |> Seq.map EmbeddingDataId)
            match result with
            | Ok () -> return this.Ok() :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search")>]
    member this.SearchSimilar([<FromBody>] request: {| model: string; vector: float32[]; tenantId: Guid; limit: int |}) =
        task {
            let embedding = { Model = request.model; Vector = request.vector }
            let! result = vectorDbService.SearchSimilarEmbeddingsAsync(embedding, TenantId request.tenantId, request.limit)
            match result with
            | Ok results -> 
                let response = 
                    results 
                    |> Seq.map (fun (data, BookId bookIdGuid) -> 
                        {| model = data.Model; vector = data.Vector; bookId = bookIdGuid |})
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search-with-score")>]
    member this.SearchSimilarWithScore([<FromBody>] request: {| model: string; vector: float32[]; tenantId: Guid; limit: int; threshold: float option |}) =
        task {
            let embedding = { Model = request.model; Vector = request.vector }
            let! result = vectorDbService.SearchSimilarEmbeddingsWithScoreAsync(embedding, TenantId request.tenantId, request.limit, ?threshold = request.threshold)
            match result with
            | Ok results -> 
                let response = 
                    results 
                    |> Seq.map (fun (data, BookId bookIdGuid, score) -> 
                        {| model = data.Model; vector = data.Vector; bookId = bookIdGuid; score = score |})
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search-filtered")>]
    member this.SearchSimilarFiltered([<FromBody>] request: {| model: string; vector: float32[]; tenantId: Guid; bookIds: seq<Guid>; limit: int |}) =
        task {
            let embedding = { Model = request.model; Vector = request.vector }
            let bookIds = request.bookIds |> Seq.map BookId |> Seq.toList
            let! result = vectorDbService.SearchSimilarEmbeddingsFilteringByBookIdsAsync(embedding, bookIds, TenantId request.tenantId, request.limit)
            match result with
            | Ok results -> 
                let response = 
                    results 
                    |> Seq.map (fun (data, BookId bookIdGuid) -> 
                        {| model = data.Model; vector = data.Vector; bookId = bookIdGuid |})
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("search-with-score-filtered")>]
    member this.SearchSimilarWithScoreFiltered([<FromBody>] request: {| model: string; vector: float32[]; tenantId: Guid; bookIds: seq<Guid>; limit: int; threshold: float option |}) =
        task {
            let embedding = { Model = request.model; Vector = request.vector }
            let bookIds = request.bookIds |> Seq.map BookId |> Seq.toList
            let! result = vectorDbService.SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync(embedding, bookIds, TenantId request.tenantId, request.limit, ?threshold = request.threshold)
            match result with
            | Ok results -> 
                let response = 
                    results 
                    |> Seq.map (fun (data, BookId bookIdGuid, score) -> 
                        {| model = data.Model; vector = data.Vector; bookId = bookIdGuid; score = score |})
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpGet("all-ids")>]
    member this.ReadAllIds([<FromQuery>] tenantId: Guid) =
        task {
            let! result = vectorDbService.ReadAllEmbeddingIdsWithBookIdsAsync(TenantId tenantId)
            match result with
            | Ok results -> 
                let response = 
                    results 
                    |> Seq.map (fun (EmbeddingDataId id, BookId bookIdGuid) -> 
                        {| id = id; bookId = bookIdGuid |})
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("enquiry-missing")>]
    member this.EnquiryMissing([<FromBody>] ids: seq<Guid>) =
        task {
            let embeddingIds = ids |> Seq.map EmbeddingDataId |> Seq.toList
            let! result = vectorDbService.EnquiryForMissingEmbeddingsAsync(embeddingIds)
            match result with
            | Ok missingIds -> 
                let response = missingIds |> List.map (fun (EmbeddingDataId id) -> id)
                return this.Ok(response) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
