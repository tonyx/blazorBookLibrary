
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

    member this.StoreEmbeddingAsync (embeddingDataId: EmbeddingDataId, tenantId: TenantId, bookId: BookId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "INSERT INTO item_embeddings_projections (id, tenant_id, book_id, vector_data, model_name, created_at, last_updated_at) 
                   VALUES (@id, @tenant_id, @book_id, @vector_data::real[]::vector, @model_name, @created_at, @last_updated_at)"
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
                        "vector_data", Sql.doubleArray (embeddingData.Vector |> Array.map float)
                        "model_name", Sql.string embeddingData.Model
                        "created_at", Sql.timestamp DateTime.Now
                        "last_updated_at", Sql.timestamp DateTime.Now
                    ]
                    |> Sql.executeNonQueryAsync // cts.Token
                    |> TaskResult.ofTask
                    |> TaskResult.mapError (fun e -> e.Message)
                
                return Ok ()
            with
            | ex -> return Error ex.Message
        }

    member this.ReadEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId, string>> =
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id FROM item_embeddings_projections WHERE id = @id"
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
                        {
                            Model = read.string "model_name"
                            Vector = read.doubleArray "vector_data" |> Array.map float32
                        }, BookId (read.uuid "book_id")
                    )
                
                match result |> List.tryHead with
                | Some x -> return Ok x
                | None -> return Error $"Embedding not found for id {embeddingDataId.Value}"
            with
            | ex -> return Error ex.Message
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
                   FROM item_embeddings_projections 
                   WHERE tenant_id = @tenant_id
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
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id, 
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
                   FROM item_embeddings_projections 
                   WHERE tenant_id = @tenant_id
                   AND book_id = ANY(@book_ids)
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
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id, 
                   (1 - (vector_data <=> @vector_data::real[]::vector)) as score
                   FROM item_embeddings_projections 
                   WHERE tenant_id = @tenant_id
                   AND book_id = ANY(@book_ids)
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
    member this.ReadAllEmbeddingIdsWithBookIdsAsync(tenantId: TenantId, ?ct: CancellationToken): Task<Result< seq<EmbeddingDataId * BookId>, string>> = 
        let sql = "SELECT id, book_id FROM item_embeddings_projections WHERE tenant_id = @tenant_id"
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

    interface IVectorDbService with
        member this.StoreEmbeddingAsync (embeddingDataId: EmbeddingDataId, tenantId: TenantId, bookId: BookId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.StoreEmbeddingAsync (embeddingDataId, tenantId, bookId, embeddingData, ct)

        member this.ReadEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.ReadEmbeddingAsync (embeddingDataId, ct)

        member this.UpdateEmbeddingAsync (embeddingDataId: EmbeddingDataId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            this.UpdateEmbeddingAsync (embeddingDataId, embeddingData, ?ct = ct)

        member this.RemoveEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveEmbeddingAsync (embeddingDataId, ct)

        member this.RemoveEmbeddingsAsync (embeddingDataIds: seq<EmbeddingDataId>, ?ct: CancellationToken) : Task<Result<unit, string>> =
            this.RemoveEmbeddingsAsync (embeddingDataIds, ?ct = ct)

        member this.SearchSimilarEmbeddingsAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsAsync (embeddingData, tenantId, limit, ct)

        member this.SearchSimilarEmbeddingsWithScoreAsync (embeddingData: EmbeddingData, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId * float>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsWithScoreAsync (embeddingData, tenantId, limit, ?threshold = threshold, ct = ct)

        member this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, tenantId: TenantId, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData, bookIds, tenantId, limit, ct)

        member this.SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, tenantId: TenantId, limit: int, ?threshold: float, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId * float>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync (embeddingData, bookIds, tenantId, limit, ?threshold = threshold, ct = ct)        
        member this.ReadAllEmbeddingIdsWithBookIdsAsync(tenantId: TenantId, ?ct: CancellationToken): Task<Result<(EmbeddingDataId * BookId) seq,string>> = 
            let ct = defaultArg ct CancellationToken.None
            this.ReadAllEmbeddingIdsWithBookIdsAsync (tenantId, ct)

        member this.EnquiryForMissingEmbeddingsAsync (embeddingDataIds: List<EmbeddingDataId>, ?ct: CancellationToken) : Task<Result<List<EmbeddingDataId>, string>> =
            this.EnquiryForMissingEmbeddingsAsync (embeddingDataIds, ?ct = ct)


