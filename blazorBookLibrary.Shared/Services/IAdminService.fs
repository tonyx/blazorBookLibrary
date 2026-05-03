
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open System.Runtime.InteropServices

type IAdminServices = 
    abstract member PurgeVectorsReferringDroppedBooksAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>
    abstract member AdjustBookStatesReferringMissingEmbeddingsAsync: [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<unit, string>>