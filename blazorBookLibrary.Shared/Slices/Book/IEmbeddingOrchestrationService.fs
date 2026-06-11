namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type IEmbeddingOrchestrationService = 
    abstract member CreateEmbeddingForBookAsync: context: UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> TaskResult<unit, string>
    abstract member CreateEmbeddingsForBooksIfMissingAsync: context: UserContext * bookIds: List<BookId> * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> TaskResult<unit, string>
