namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open System

type IVectorDbService = 
    abstract member StoreEmbeddingAsync: EmbeddingDataId * BookId * EmbeddingData * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ReadEmbeddingAsync: EmbeddingDataId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<EmbeddingData * BookId, string>>
    abstract member UpdateEmbeddingAsync: EmbeddingDataId * EmbeddingData * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveEmbeddingAsync: EmbeddingDataId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SearchSimilarEmbeddingsAsync: EmbeddingData * int * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId>, string>>
    abstract member SearchSimilarEmbeddingsWithScoreAsync: EmbeddingData * int * [<Optional; DefaultParameterValue(null)>] ?threshold:float * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId * float>, string>>
    abstract member SearchSimilarEmbeddingsFilteringByBookIdsAsync: EmbeddingData * List<BookId> * int * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId>, string>>
    abstract member SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync: EmbeddingData * List<BookId> * int * [<Optional; DefaultParameterValue(null)>] ?threshold:float * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId * float>, string>>
