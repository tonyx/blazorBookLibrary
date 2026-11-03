namespace BookLibrary.Shared.Services

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Formats

type ICatalogImportService =
    abstract member ImportCatalogAsync:
        context: UserContext *
        payload: string *
        format: ExportFormat *
        [<Optional>] ?options: CatalogImportOptions *
        [<Optional; DefaultParameterValue(null: obj)>] ?progress: IProgress<ImportProgress> *
        [<Optional; DefaultParameterValue(null: obj)>] ?ct: CancellationToken -> Task<Result<CatalogImportResult, string>>
