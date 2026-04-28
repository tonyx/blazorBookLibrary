
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
        let connectionString = configuration.GetConnectionString "VectorDbConnection"
        let timeout = configuration.GetValue<int>("CancellationTokenSourceExpiration", 100000)
        VectorDbService (connectionString, timeout)

    member this.StoreEmbeddingAsync (embeddingDataId: EmbeddingDataId, bookId: BookId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
        let sql = "INSERT INTO item_embeddings_projections (id, book_id, vector_data, model_name, last_updated_at) 
                   VALUES (@id, @book_id, @vector_data::real[]::vector, @model_name, @last_updated_at)
                   ON CONFLICT (id) DO UPDATE 
                   SET book_id = @book_id, vector_data = @vector_data::real[]::vector, model_name = @model_name, last_updated_at = @last_updated_at"
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
                        "book_id", Sql.uuid bookId.Value
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

    member this.SearchSimilarEmbeddingsAsync (embeddingData: EmbeddingData, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id 
                   FROM item_embeddings_projections 
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

    member this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
        let sql = "SELECT (vector_data::real[])::float8[] as vector_data, model_name, book_id 
                   FROM item_embeddings_projections 
                   WHERE book_id = ANY(@book_ids)
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

    interface IVectorDbService with
        member this.StoreEmbeddingAsync (embeddingDataId: EmbeddingDataId, bookId: BookId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.StoreEmbeddingAsync (embeddingDataId, bookId, embeddingData, ct)

        member this.ReadEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<EmbeddingData * BookId, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.ReadEmbeddingAsync (embeddingDataId, ct)

        member this.UpdateEmbeddingAsync (embeddingDataId: EmbeddingDataId, embeddingData: EmbeddingData, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.UpdateEmbeddingAsync (embeddingDataId, embeddingData, ct)

        member this.RemoveEmbeddingAsync (embeddingDataId: EmbeddingDataId, ?ct: CancellationToken) : Task<Result<unit, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.RemoveEmbeddingAsync (embeddingDataId, ct)

        member this.SearchSimilarEmbeddingsAsync (embeddingData: EmbeddingData, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsAsync (embeddingData, limit, ct)

        member this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData: EmbeddingData, bookIds: List<BookId>, limit: int, ?ct: CancellationToken) : Task<Result<seq<EmbeddingData * BookId>, string>> =
            let ct = defaultArg ct CancellationToken.None
            this.SearchSimilarEmbeddingsFilteringByBookIdsAsync (embeddingData, bookIds, limit, ct)


