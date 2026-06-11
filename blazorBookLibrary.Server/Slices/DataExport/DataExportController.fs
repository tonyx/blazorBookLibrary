
namespace BookLibrary.Controllers

open Microsoft.AspNetCore.Mvc
open BookLibrary.Services
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Domain
open System.Threading.Tasks
open System
open System.Collections.Generic

[<ApiController>]
[<Route("api/[controller]")>]
type DataExportController(dataExportService: IDataExportService) =
    inherit ControllerBase()

    [<HttpGet("export/books")>]
    member this.ExportAllBooks([<FromQuery>] format: string) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let exportFormat = 
                match format.ToLower() with
                | "csv" -> Csv
                | _ -> Json
            let! result = dataExportService.ExportAllBooksAsync(context, exportFormat)
            match result with
            | Ok content -> 
                let contentType = if exportFormat = Csv then "text/csv" else "application/json"
                return this.Content(content, contentType) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }

    [<HttpPost("import/isbns")>]
    member this.ImportFromIsbns([<FromBody>] isbns: string list, [<FromQuery>] preventDuplicates: bool, [<FromQuery>] generateUnknownAuthors: bool, [<FromQuery>] generateEmbeddings: bool, [<FromQuery>] generateMissingDescriptions: bool) =
        task {
            let context = UserContextMapper.mapFromClaimsPrincipal this.User
            let isbnList = isbns |> List.choose (fun s -> match Isbn.New s with | Ok i -> Some i | _ -> None)
            // Progress reporting is omitted for simplicity in this basic REST wrapper
            let! result = dataExportService.ImportFromIsbns(context, isbnList, preventDuplicates, generateUnknownAuthors, generateEmbeddings, generateMissingDescriptions, null, System.Threading.CancellationToken.None)
            match result with
            | Ok summary -> return this.Ok(summary) :> IActionResult
            | Error msg -> return this.BadRequest(msg) :> IActionResult
        }
