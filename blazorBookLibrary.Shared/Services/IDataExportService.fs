
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
    }

type IDataExportService =
    abstract member ExportAllBooksAsync: exportFormat: ExportFormat * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<string, string>>
    abstract member ImportFromIsbns: isdns: List<Isbn> * preventDuplicates:bool * generateUnknownAuthors: bool * [<Optional; DefaultParameterValue(null)>] progress: IProgress<ImportProgress> * [<Optional; DefaultParameterValue(null)>] ct: CancellationToken -> Task<Result<int*int, string>>
