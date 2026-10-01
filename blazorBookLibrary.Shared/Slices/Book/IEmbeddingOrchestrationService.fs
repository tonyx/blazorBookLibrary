namespace BookLibrary.Shared.Services

open System.Threading
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Shared.Commons

type IEmbeddingOrchestrationService = 
    abstract member CreateEmbeddingForBookAsync: context: UserContext * bookId: BookId * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> TaskResult<unit, string>
    abstract member CreateEmbeddingsForBooksIfMissingAsync: context: UserContext * bookIds: List<BookId> * [<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken -> TaskResult<unit, string>
    abstract member CreateEmbeddingForPaperAsync: context: UserContext * bookId: BookId * paperId: PaperId * text: string * [<Optional; DefaultParameterValue(null)>] ?storeDescription: bool * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>
    abstract member RemoveEmbeddingForPaperAsync: context: UserContext * bookId: BookId * paperId: PaperId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> TaskResult<unit, string>

