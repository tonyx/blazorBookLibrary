
namespace BookLibrary.Shared.Services

open System
open System.Runtime.InteropServices
open System.Threading
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type ImportProgress = 
    {
        Current: int
        Total: int
        EstimatedRemainingTime: TimeSpan option
        CurrentItemLabel: string option
    }

type ImportStatus = Success | Failure of reason: string | Duplicate | Interrupted

type ImportItemDetail = {
    Isbn: Isbn
    Status: ImportStatus
    Title: string option
}

type ImportSummary = {
    Details: ImportItemDetail list
    TotalProcessed: int
    SuccessCount: int
    FailureCount: int
    DuplicateCount: int
    InterruptedCount: int
}

type IDataExportService =
    abstract member ExportAllBooksAsync: exportFormat: ExportFormat * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string, string>>
    abstract member ImportFromIsbns: isdns: List<Isbn> * preventDuplicates:bool * generateUnknownAuthors: bool * [<Optional; DefaultParameterValue(false)>]generateEmbeddings: bool * [<Optional; DefaultParameterValue(false)>]generateMissingDescriptions: bool * [<Optional; DefaultParameterValue(null)>] progress: IProgress<ImportProgress> * [<Optional; DefaultParameterValue(null)>] ct: CancellationToken -> Task<Result<ImportSummary, string>>
