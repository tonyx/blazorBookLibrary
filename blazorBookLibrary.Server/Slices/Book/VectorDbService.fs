
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open Npgsql.FSharp
open Npgsql
open FSharpPlus
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open Microsoft.Extensions.DependencyInjection
open BookLibrary.Services.UserMapping
open BookLibrary.Utils

type VectorDbService(connection: string, ?cancellationTokenSourceExpiration: int) =
    let cancellationTokenSourceExpiration = defaultArg cancellationTokenSourceExpiration 100000

    new (configuration: IConfiguration, secretsReader: SecretsReader) =
        let connectionString = secretsReader.GetVectorDbConnectionString ()
        // let connectionString = configuration.GetConnectionString "VectorDbConnection"
        let timeout = configuration.GetValue<int>("CancellationTokenSourceExpiration", 100000)
        VectorDbService (connectionString, timeout)

    member this.StoreEmbeddingAsync (
        embeddingDataId: EmbeddingDataId, 
        tenantId: TenantId, 
        bookId: BookId, 
        embeddingData: EmbeddingData, 
        ?paperId: PaperId,
        ?itemType: string,
        ?tags: string list,
        ?relatedEmbeddingIds: EmbeddingDataId list,
        ?ct: CancellationToken
    ) : Task<Result<unit, string>> =
        let itemTypeStr = defaultArg itemType (if paperId.IsSome then "paper" else "book")
        let tagsArray = defaultArg tags [] |> List.toArray
        let relatedIdsArray = defaultArg relatedEmbeddingIds [] |> List.map (fun id -> id.Value) |> List.toArray
        let paperIdGuid = paperId |> Option.map (fun p -> p.Value)

        let sql = "INSERT INTO item_embeddings_projections (id, tenant_id, book_id, paper_id, vector_data, model_name, item_type, tags, related_embedding_ids, created_at, last_updated_at) 
                   VALUES (@id, @tenant_id, @book_id, @paper_id, @vector_data::real[]::vector, @model_name, @item_type, @tags, @related_embedding_ids, @created_at, @last_updated_at)"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "id", Sql.uuid embeddingDataId.Value
                        "tenant_id", Sql.uuid tenantId.Value
                        "book_id", Sql.uuid bookId.Value
                        "paper_id", Sql.uuidOrNone paperIdGuid
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "model_name", Sql.string embeddingData.Model
                        "item_type", Sql.string itemTypeStr
                        "tags", Sql.stringArray tagsArray
                        "related_embedding_ids", Sql.uuidArray relatedIdsArray
                        "created_at", Sql.timestamp DateTime.Now
                        "last_updated_at", Sql.timestamp DateTime.Now
                    ]
                    |> Sql.executeNonQueryAsync
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)
                
                return Ok ()
            with
            | ex -> return Error ex.Message
        }

    member this.StorePaperEmbeddingAsync (embeddingDataId: EmbeddingDataId, tenantId: TenantId, bookId: BookId, paperId: PaperId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let ct = defaultArg ct CancellationToken.None
        this.StoreEmbeddingAsync (embeddingDataId, tenantId, bookId, embeddingData, paperId = paperId, itemType = "paper", ct = ct)

    member this.ReadEmbeddingWithDetailsAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId * Option<PaperId>, string>> =
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id, paper_id FROM item_embeddings_projections WHERE id = @id"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)

                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ "id", Sql.uuid embeddingDataId.Value ]
                    |> Sql.executeAsync (fun read ->
                        let paperId = read.uuidOrNone "paper_id" |> Option.map PaperId
                        {
                            Model = read.string "model_name"
                            Vector = read.doubleArray "vector_data" |> Array.map float32
                        }, BookId (read.uuid "book_id"), paperId
                    )
                
                match result |> List.tryHead with
                | Some x -> return Ok x
                | None -> return Error $"Embedding not found for id {embeddingDataId.Value}"
            with
            | ex -> return Error ex.Message
        }

    member this.ReadEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId, string>> =
        task {
            let ct = defaultArg ct CancellationToken.None
            let! res = this.ReadEmbeddingWithDetailsAsync (embeddingDataId, ct = ct)
            return res |> Result.map (fun (data, bookId, _) -> data, bookId)
        }
    member this.UpdateEmbeddingAsync (embeddingDataId: EmbeddingDataId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "UPDATE item_embeddings_projections 
                   SET vector_data = @vector_data::real[]::vector, model_name = @model_name, last_updated_at = @last_updated_at 
                   WHERE id = @id"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)

                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [
                        "id", Sql.uuid embeddingDataId.Value
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "model_name", Sql.string embeddingData.Model
                        "last_updated_at", Sql.timestamp DateTime.Now
                    ]
                    |> Sql.executeNonQueryAsync //  cts.Token
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)

                return Ok () 
            with
            | ex -> return Error ex.Message
        }

    member this.RemoveEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "DELETE FROM item_embeddings_projections WHERE id = @id"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)

                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ "id", Sql.uuid embeddingDataId.Value ]
                    |> Sql.executeNonQueryAsync // cts.Token
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)
                
                return Ok ()
            with
            | ex -> return Error ex.Message
        }
    member this.RemoveEmbeddingsAsync (embeddingDataIds: seq<EmbeddingDataId>, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "DELETE FROM item_embeddings_projections WHERE id = ANY(@ids)"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)

                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ "ids", Sql.uuidArray (embeddingDataIds |> Seq.map (fun id -> id.Value) |> Array.ofSeq) ]
                    |> Sql.executeNonQueryAsync // cts.Token
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)
                
                return Ok ()
            with
            | ex -> return Error ex.Message
        }

    member this.SearchSimilarEmbeddingsAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id 
                   FROM (
                       SELECT DISTINCT ON (book_id)
                           vector_data,
                           model_name,
                           book_id,
                           (vector_data <=> @vector_data::real[]::vector) as distance
                       FROM item_embeddings_projections 
                       WHERE tenant_id = @tenant_id
                       ORDER BY book_id, vector_data <=> @vector_data::real[]::vector
                   ) sub
                   ORDER BY distance ASC
                   LIMIT @limit"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "limit", Sql.int limit
                    ]
                    |> Sql.executeAsync (fun read ->
                        {
                            Model = read.string "model_name"
                            Vector = read.doubleArray "vector_data" |> Array.map float32
                        }, BookId (read.uuid "book_id")
                    )
                
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }

    member this.SearchSimilarEmbeddingsWithScoreAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId * float>, string>> =
        let threshold = defaultArg threshold -1.0 // default to no threshold (score is in [ -1, 1 ] for cosine similarity, actually [0, 2] distance so [-1, 1] similarity)
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id, score
                   FROM (
                       SELECT DISTINCT ON (book_id)
                           vector_data,
                           model_name,
                           book_id,
                           (1 - (vector_data <=> @vector_data::real[]::vector)) as score,
                           (vector_data <=> @vector_data::real[]::vector) as distance
                       FROM item_embeddings_projections 
                       WHERE tenant_id = @tenant_id
                       AND (1 - (vector_data <=> @vector_data::real[]::vector)) >= @threshold
                       ORDER BY book_id, vector_data <=> @vector_data::real[]::vector
                   ) sub
                   ORDER BY distance ASC
                   LIMIT @limit"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "limit", Sql.int limit
                        "threshold", Sql.double threshold
                    ]
                    |> Sql.executeAsync (fun read ->
                        {
                            Model = read.string "model_name"
                            Vector = read.doubleArray "vector_data" |> Array.map float32
                        }, BookId (read.uuid "book_id"), read.double "score"
                    )
                
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }

    member this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, tenantId: TenantId, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id 
                   FROM (
                       SELECT DISTINCT ON (book_id)
                           vector_data,
                           model_name,
                           book_id,
                           (vector_data <=> @vector_data::real[]::vector) as distance
                       FROM item_embeddings_projections 
                       WHERE tenant_id = @tenant_id
                       AND book_id = ANY(@book_ids)
                       ORDER BY book_id, vector_data <=> @vector_data::real[]::vector
                   ) sub
                   ORDER BY distance ASC
                   LIMIT @limit"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "book_ids", Sql.uuidArray (bookIds |> List.map (fun b -> b.Value) |> Array.ofList)
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "limit", Sql.int limit
                    ]
                    |> Sql.executeAsync (fun read ->
                        {
                            Model = read.string "model_name"
                            Vector = read.doubleArray "vector_data" |> Array.map float32
                        }, BookId (read.uuid "book_id")
                    )
                
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }

    member this.SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId * float>, string>> =
        let threshold = defaultArg threshold -1.0
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id, score
                   FROM (
                       SELECT DISTINCT ON (book_id)
                           vector_data,
                           model_name,
                           book_id,
                           (1 - (vector_data <=> @vector_data::real[]::vector)) as score,
                           (vector_data <=> @vector_data::real[]::vector) as distance
                       FROM item_embeddings_projections 
                       WHERE tenant_id = @tenant_id
                       AND book_id = ANY(@book_ids)
                       AND (1 - (vector_data <=> @vector_data::real[]::vector)) >= @threshold
                       ORDER BY book_id, vector_data <=> @vector_data::real[]::vector
                   ) sub
                   ORDER BY distance ASC
                   LIMIT @limit"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "book_ids", Sql.uuidArray (bookIds |> List.map (fun b -> b.Value) |> Array.ofList)
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "limit", Sql.int limit
                        "threshold", Sql.double threshold
                    ]
                    |> Sql.executeAsync (fun read ->
                        {
                            Model = read.string "model_name"
                            Vector = read.doubleArray "vector_data" |> Array.map float32
                        }, BookId (read.uuid "book_id"), read.double "score"
                    )
                
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }
    member this.RemoveEmbeddingsByBookIdAsync (tenantId: TenantId, bookId: BookId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "DELETE FROM item_embeddings_projections WHERE tenant_id = @tenant_id AND book_id = @book_id"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)

                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "book_id", Sql.uuid bookId.Value 
                    ]
                    |> Sql.executeNonQueryAsync
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)
                
                return Ok ()
            with
            | ex -> return Error ex.Message
        }

    member this.SearchSimilarDetailedAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<VectorDbSearchResult>, string>> =
        let threshold = defaultArg threshold -1.0
        let sql = "SELECT id, (vector_data::real[])::float8[] as vector_data, model_name, book_id, paper_id, 
                   coalesce(item_type, 'book') as item_type,
                   (1 - (vector_data <=> @vector_data::real[]::vector)) as score
                   FROM item_embeddings_projections 
                   WHERE tenant_id = @tenant_id
                   AND (1 - (vector_data <=> @vector_data::real[]::vector)) >= @threshold
                   ORDER BY vector_data <=> @vector_data::real[]::vector
                   LIMIT @limit"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "limit", Sql.int limit
                        "threshold", Sql.double threshold
                    ]
                    |> Sql.executeAsync (fun read ->
                        let paperId = read.uuidOrNone "paper_id" |> Option.map PaperId
                        {
                            Id = EmbeddingDataId (read.uuid "id")
                            Embedding = {
                                Model = read.string "model_name"
                                Vector = read.doubleArray "vector_data" |> Array.map float32
                            }
                            BookId = BookId (read.uuid "book_id")
                            PaperId = paperId
                            Score = read.double "score"
                            ItemType = read.string "item_type"
                        }
                    )
                
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }

    member this.ReadAllEmbeddingIdsWithBookIdsAsync(tenantId: TenantId, ?ct: CancellationToken): Task<Result< seq<EmbeddingDataId * BookId>, string>> = 
        let sql = "SELECT id, book_id FROM item_embeddings_projections WHERE tenant_id = @tenant_id AND paper_id IS NULL"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ "tenant_id", Sql.uuid tenantId.Value ]
                    |> Sql.executeAsync (fun read ->
                        EmbeddingDataId (read.uuid "id"), BookId (read.uuid "book_id")
                    )
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }

    member this.ReadAllPaperEmbeddingIdsAsync(tenantId: TenantId, ?ct: CancellationToken): Task<Result< seq<EmbeddingDataId * BookId * PaperId>, string>> = 
        let sql = "SELECT id, book_id, paper_id FROM item_embeddings_projections WHERE tenant_id = @tenant_id AND paper_id IS NOT NULL"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                
                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ "tenant_id", Sql.uuid tenantId.Value ]
                    |> Sql.executeAsync (fun read ->
                        EmbeddingDataId (read.uuid "id"), BookId (read.uuid "book_id"), PaperId (read.uuid "paper_id")
                    )
                return Ok (result |> Seq.ofList)
            with
            | ex -> return Error ex.Message
        }

    member this.EnquiryForMissingEmbeddingsAsync (embeddingDataIds: List<EmbeddingDataId>, ?ct: CancellationToken): Task<Result<List<EmbeddingDataId>, string>> = 
        let sql = "SELECT id FROM item_embeddings_projections WHERE id = ANY(@embedding_data_ids)"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)
                let! existingIdsList = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ "embedding_data_ids", Sql.uuidArray (embeddingDataIds |> Seq.map (fun id -> id.Value) |> Array.ofSeq) ]
                    |> Sql.executeAsync (fun read ->
                        EmbeddingDataId (read.uuid "id")
                    )
                let existingIds = existingIdsList |> Set.ofList
                let missingIds = 
                    embeddingDataIds 
                    |> List.filter (fun id -> not (existingIds.Contains id))
                return Ok missingIds
            with
            | ex -> return Error ex.Message
        }

    member this.LinkIntraBookEmbeddingsAsync (tenantId: TenantId, bookId: BookId, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "WITH book_embeddings AS (
                       SELECT id FROM item_embeddings_projections 
                       WHERE tenant_id = @tenant_id AND book_id = @book_id
                   )
                   UPDATE item_embeddings_projections p
                   SET related_embedding_ids = ARRAY(
                       SELECT be.id FROM book_embeddings be WHERE be.id <> p.id
                   )
                   WHERE p.tenant_id = @tenant_id AND p.book_id = @book_id"
        task {
            try
                let ct = defaultArg ct CancellationToken.None
                use cts = CancellationTokenSource.CreateLinkedTokenSource (ct)
                cts.CancelAfter(cancellationTokenSourceExpiration)

                let! result = 
                    connection
                    |> Sql.connect
                    |> Sql.query sql
                    |> Sql.parameters [ 
                        "tenant_id", Sql.uuid tenantId.Value
                        "book_id", Sql.uuid bookId.Value 
                    ]
                    |> Sql.executeNonQueryAsync
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)
                
                return Ok ()
            with
            | ex -> return Error ex.Message
        }

    interface IVectorDbService with
        member this.StoreEmbeddingAsync (embeddingDataId: EmbeddingDataId, tenantId: TenantId, bookId: BookId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.StoreEmbeddingAsync (embeddingDataId, tenantId, bookId, embeddingData, ct = ct)

        member this.StorePaperEmbeddingAsync (embeddingDataId: EmbeddingDataId, tenantId: TenantId, bookId: BookId, paperId: PaperId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.StorePaperEmbeddingAsync (embeddingDataId, tenantId, bookId, paperId, embeddingData, ct = ct)

        member this.ReadEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.ReadEmbeddingAsync (embeddingDataId, ct = ct)

        member this.ReadEmbeddingWithDetailsAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId * Option<PaperId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.ReadEmbeddingWithDetailsAsync (embeddingDataId, ct = ct)

        member this.UpdateEmbeddingAsync (embeddingDataId: EmbeddingDataId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            this.UpdateEmbeddingAsync (embeddingDataId, embeddingData, ?ct = ct)

        member this.RemoveEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveEmbeddingAsync (embeddingDataId, ct)

        member this.RemoveEmbeddingsAsync (embeddingDataIds: seq<EmbeddingDataId>, ?ct: CancellationToken) : Task<Result<unit, string>> =
            this.RemoveEmbeddingsAsync (embeddingDataIds, ?ct = ct)

        member this.RemoveEmbeddingsByBookIdAsync (tenantId: TenantId, bookId: BookId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveEmbeddingsByBookIdAsync (tenantId, bookId, ct = ct)

        member this.SearchSimilarEmbeddingsAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsAsync (embeddingData, tenantId, limit, ct)

        member this.SearchSimilarEmbeddingsWithScoreAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId * float>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsWithScoreAsync (embeddingData, tenantId, limit, ?threshold = threshold, ct = ct)

        member this.SearchSimilarDetailedAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<VectorDbSearchResult>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarDetailedAsync (embeddingData, tenantId, limit, ?threshold = threshold, ct = ct)

        member this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, tenantId: TenantId, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData, bookIds, tenantId, limit, ct)

        member this.SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId * float>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync (embeddingData, bookIds, tenantId, limit, ?threshold = threshold, ct = ct)        

        member this.ReadAllEmbeddingIdsWithBookIdsAsync(tenantId: TenantId, ?ct: CancellationToken): Task<Result<(EmbeddingDataId * BookId) seq,string>> = 
            let ct = defaultArg ct CancellationToken.None
            this.ReadAllEmbeddingIdsWithBookIdsAsync (tenantId, ct)

        member this.ReadAllPaperEmbeddingIdsAsync(tenantId: TenantId, ?ct: CancellationToken): Task<Result<(EmbeddingDataId * BookId * PaperId) seq,string>> = 
            let ct = defaultArg ct CancellationToken.None
            this.ReadAllPaperEmbeddingIdsAsync (tenantId, ct)

        member this.LinkIntraBookEmbeddingsAsync(tenantId: TenantId, bookId: BookId, ?ct: CancellationToken): Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.LinkIntraBookEmbeddingsAsync (tenantId, bookId, ct = ct)

        member this.EnquiryForMissingEmbeddingsAsync (embeddingDataIds: List<EmbeddingDataId>, ?ct: CancellationToken) : Task<Result<List<EmbeddingDataId>, string>> =
            this.EnquiryForMissingEmbeddingsAsync (embeddingDataIds, ?ct = ct)


