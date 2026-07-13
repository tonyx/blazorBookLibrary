namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons

type IVectorDbService = 
    abstract member StoreEmbeddingAsync: EmbeddingDataId * TenantId * BookId * EmbeddingData * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ReadEmbeddingAsync: EmbeddingDataId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<EmbeddingData * BookId, string>>
    abstract member UpdateEmbeddingAsync: EmbeddingDataId * EmbeddingData * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveEmbeddingAsync: EmbeddingDataId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member RemoveEmbeddingsAsync: seq<EmbeddingDataId> * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member SearchSimilarEmbeddingsAsync: EmbeddingData * TenantId * int * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId>, string>>
    abstract member SearchSimilarEmbeddingsWithScoreAsync: EmbeddingData * TenantId * int * [<Optional; DefaultParameterValue(null)>] ?threshold:float * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId * float>, string>>
    abstract member SearchSimilarEmbeddingsFilteringByBookIdsAsync: EmbeddingData * List<BookId> * TenantId * int * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId>, string>>
    abstract member SearchSimilarEmbeddingsWithScoreFilteringByBookIdsAsync: EmbeddingData * List<BookId> * TenantId * int * [<Optional; DefaultParameterValue(null)>] ?threshold:float * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingData * BookId * float>, string>>
    abstract member ReadAllEmbeddingIdsWithBookIdsAsync: TenantId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result< seq<EmbeddingDataId * BookId>, string>>
    abstract member EnquiryForMissingEmbeddingsAsync: List<EmbeddingDataId> * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> Task<Result<List<EmbeddingDataId>, string>>
