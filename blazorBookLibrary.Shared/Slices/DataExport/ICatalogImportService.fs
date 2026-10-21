namespace BookLibrary.Shared.Services

open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Formats

type ICatalogImportService =
    abstract member ImportCatalogAsync: context: UserContext * payload: string * format: ExportFormat * [<Optional>] ?options: CatalogImportOptions * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken -> Task<Result<CatalogImportResult, string>>
