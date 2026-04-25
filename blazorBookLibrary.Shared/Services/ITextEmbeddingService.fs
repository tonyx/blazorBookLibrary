
namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open blazorBookLibrary.Data

type ITextEmbeddingService = 
    abstract member GetEmbeddingAsync: string -> Task<Result<EmbeddingData,string>>