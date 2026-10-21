namespace BookLibrary.Controllers

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic
open Microsoft.AspNetCore.Mvc
open Microsoft.AspNetCore.RateLimiting
open Microsoft.Extensions.Configuration
open BookLibrary.Domain
open BookLibrary.Services
open BookLibrary.Shared
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Shared.Formats
open BookLibrary.Shared.Services
open FsToolkit.ErrorHandling

[<ApiController>]
[<Route("api/catalog")>]
type CatalogController
    (
        bookService: IBookService,
        detailsService: IDetailsService,
        editorService: IEditorService,
        tenantService: ITenantService,
        configuration: IConfiguration,
        tenantApiKeyService: ITenantApiKeyService,
        catalogImportService: ICatalogImportService
    ) =
    inherit ControllerBase()

    new
        (
            bookService: IBookService,
            detailsService: IDetailsService,
            editorService: IEditorService,
            tenantService: ITenantService,
            configuration: IConfiguration,
            tenantApiKeyService: ITenantApiKeyService
        ) =
        CatalogController(
            bookService,
            detailsService,
            editorService,
            tenantService,
            configuration,
            tenantApiKeyService,
            Unchecked.defaultof<ICatalogImportService>
        )

    new
        (
            bookService: IBookService,
            detailsService: IDetailsService,
            editorService: IEditorService,
            tenantService: ITenantService,
            configuration: IConfiguration
        ) =
        CatalogController(
            bookService,
            detailsService,
            editorService,
            tenantService,
            configuration,
            Unchecked.defaultof<ITenantApiKeyService>,
            Unchecked.defaultof<ICatalogImportService>
        )

    member private this.TryResolveTenant(tenantParam: string) : Result<TenantId, string> =
        if String.IsNullOrWhiteSpace tenantParam || String.Equals(tenantParam, "default", StringComparison.OrdinalIgnoreCase) then
            Ok TenantId.Default
        else
            match Guid.TryParse tenantParam with
            | true, g -> Ok (TenantId g)
            | false, _ -> Error $"Invalid tenant identifier '{tenantParam}'"

    member private this.ValidateRequestApiKey(tenantId: TenantId, ct: CancellationToken) : Task<Result<bool, IActionResult>> =
        task {
            let configuredApiKey = configuration.["Catalog:ApiKey"]
            let hasConfiguredKey = not (String.IsNullOrWhiteSpace configuredApiKey)
            let headerValOpt =
                match this.Request.Headers.TryGetValue("X-API-Key") with
                | true, values when not (String.IsNullOrWhiteSpace(values.ToString())) ->
                    Some (values.ToString().Trim())
                | _ -> None

            match headerValOpt with
            | Some sentKey ->
                // 1. Check if it matches global configured master key from appsettings.json
                if hasConfiguredKey && String.Equals(sentKey, configuredApiKey, StringComparison.Ordinal) then
                    return Ok true
                // 2. Otherwise, if not default tenant and tenantApiKeyService is available, validate against tenant partner keys
                elif tenantId <> TenantId.Default && not (isNull (box tenantApiKeyService)) then
                    let! partnerKeyRes = tenantApiKeyService.ValidateCatalogKeyAsync(tenantId, CatalogApiKey sentKey, ct)
                    match partnerKeyRes with
                    | Ok () ->
                        return Ok true
                    | Error err ->
                        return Error (this.Unauthorized({| message = $"Invalid X-API-Key: {err}" |}) :> IActionResult)
                else
                    return Error (this.Unauthorized({| message = "Invalid or missing X-API-Key header" |}) :> IActionResult)
            | None ->
                // No X-API-Key header provided
                if hasConfiguredKey then
                    return Error (this.Unauthorized({| message = "Invalid or missing X-API-Key header" |}) :> IActionResult)
                else
                    return Ok false
        }

    member private this.ResolveFormat(formatParam: string) : ExportFormat =
        let param = if isNull formatParam then "" else formatParam.Trim().ToLowerInvariant()
        match param with
        | "csv" -> Csv
        | "bibtex" | "bib" -> BibTex
        | "schemaorg" | "jsonld" | "json-ld" -> SchemaOrg
        | "dublincore" | "dc" | "dublin-core" -> DublinCore
        | "marcxml" | "marc" | "mrcx" -> MarcXml
        | "mods" -> Mods
        | "onix" | "onix3" | "onix-3.0" -> Onix
        | "xml" -> DublinCore
        | "json" -> Json
        | _ ->
            let accept =
                match this.Request.Headers.TryGetValue("Accept") with
                | true, values -> values.ToString().ToLowerInvariant()
                | false, _ -> ""
            if accept.Contains("application/marcxml+xml") then MarcXml
            elif accept.Contains("application/mods+xml") then Mods
            elif accept.Contains("application/x-bibtex") || accept.Contains("text/x-bibtex") then BibTex
            elif accept.Contains("application/ld+json") then SchemaOrg
            elif accept.Contains("application/rdf+xml") || accept.Contains("application/xml") || accept.Contains("text/xml") then DublinCore
            elif accept.Contains("text/csv") then Csv
            else Json

    member private this.ResolveImportFormat(formatParam: string) : ExportFormat =
        let param = if isNull formatParam then "" else formatParam.Trim().ToLowerInvariant()
        match param with
        | "csv" -> Csv
        | "bibtex" | "bib" -> BibTex
        | "schemaorg" | "jsonld" | "json-ld" -> SchemaOrg
        | "dublincore" | "dc" | "dublin-core" -> DublinCore
        | "marcxml" | "marc" | "mrcx" -> MarcXml
        | "mods" -> Mods
        | "onix" | "onix3" | "onix-3.0" -> Onix
        | "json" -> Json
        | _ ->
            let contentType =
                match this.Request.ContentType with
                | null -> ""
                | ct -> ct.ToLowerInvariant()
            if contentType.Contains("application/marcxml+xml") then MarcXml
            elif contentType.Contains("application/mods+xml") then Mods
            elif contentType.Contains("application/x-bibtex") || contentType.Contains("text/x-bibtex") then BibTex
            elif contentType.Contains("application/ld+json") then SchemaOrg
            elif contentType.Contains("application/rdf+xml") || contentType.Contains("application/xml") || contentType.Contains("text/xml") then
                if contentType.Contains("marc") then MarcXml else DublinCore
            elif contentType.Contains("text/csv") then Csv
            else Json

    member private this.EnsureTenantAccessible(context: UserContext, tenantId: TenantId, isApiKeyAuthenticated: bool, ct: CancellationToken) =
        taskResult {
            if tenantId = TenantId.Default || isApiKeyAuthenticated then
                return ()
            else
                let! tenant = tenantService.GetTenantAsync(context, tenantId, ct)
                if not context.IsAuthenticated then
                    if not tenant.TenantVisibility.IsPublic then
                        return! Error "Tenant is private or not accessible"
                    else
                        return ()
                else
                    let userId = context.UserId.Value
                    let hasAccess =
                        context.IsInRole Role.Admin
                        || tenant.TenantVisibility.IsPublic
                        || tenant.OwnerId = userId
                        || (tenant.Patrons |> List.exists (fun (u, _) -> u = userId))
                        || (tenant.InvitedPatrons |> List.exists (fun (u, _) -> u = userId))
                    if not hasAccess then
                        return! Error "Tenant is private or not accessible"
                    else
                        return ()
        }

    member private this.GetPublisherAsync(context: UserContext, bd: BookDetails, ct: CancellationToken) =
        task {
            match bd.Book.Editor with
            | Some edId ->
                let! edRes = editorService.GetEditorAsync(context, edId, ct)
                match edRes with
                | Ok ed -> return Some ed.Name.Value
                | Error _ -> return None
            | None -> return None
        }

    member private this.FormatSingleBook(context: UserContext, bd: BookDetails, exportFormat: ExportFormat, ct: CancellationToken) =
        task {
            let! publisher = this.GetPublisherAsync(context, bd, ct)
            let content, contentType =
                match exportFormat with
                | DublinCore -> FormatSerializers.formatDublinCoreBook bd publisher, DublinCore.ContentType
                | MarcXml -> FormatSerializers.formatMarcXmlBook bd publisher, MarcXml.ContentType
                | Mods -> FormatSerializers.formatModsBook bd publisher, Mods.ContentType
                | Onix -> FormatSerializers.formatOnixBook bd publisher, Onix.ContentType
                | BibTex -> FormatSerializers.formatBibTexBook bd publisher, BibTex.ContentType
                | SchemaOrg -> FormatSerializers.formatSchemaOrgBook bd publisher, SchemaOrg.ContentType
                | Csv ->
                    let b = bd.Book
                    let header = "Id,Title,Isbn,Year,Availability,MainCategory,Authors,Publisher"
                    let authorsStr = bd.Authors |> List.map (fun a -> a.Name.Value) |> String.concat "; "
                    let pubStr = publisher |> Option.defaultValue ""
                    let row = sprintf "%s,\"%s\",%s,%d,%s,%s,\"%s\",\"%s\"" (b.BookId.Value.ToString()) (b.Title.Value.Replace("\"", "\"\"")) (b.Isbn.Value) (b.Year.Value) (b.Availability.ToString()) (b.MainCategory.ToString()) (authorsStr.Replace("\"", "\"\"")) (pubStr.Replace("\"", "\"\""))
                    $"{header}\n{row}", Csv.ContentType
                | Json ->
                    let b = bd.Book
                    let dto =
                        {|
                            Id = b.BookId.Value
                            Title = b.Title.Value
                            Isbn = b.Isbn.Value
                            SbnCode = b.SbnCode |> Option.map (fun s -> s.Value)
                            Year = b.Year.Value
                            Authors = bd.Authors |> List.map (fun a -> a.Name.Value)
                            Publisher = publisher
                            Availability = b.Availability.ToString()
                            MainCategory = b.MainCategory.ToString()
                            Description = b.Description
                            ImageUrl = b.ImageUrl |> Option.map (fun u -> u.ToString())
                            Tags = b.Tags |> List.map (fun t -> t.TagName)
                        |}
                    System.Text.Json.JsonSerializer.Serialize(dto, System.Text.Json.JsonSerializerOptions(WriteIndented = true)), Json.ContentType
            return this.Content(content, contentType) :> IActionResult
        }

    member private this.FormatMultipleBooks(context: UserContext, bookDetailsList: BookDetails list, exportFormat: ExportFormat, ct: CancellationToken) =
        task {
            let itemsWithPublishers = ResizeArray()
            for bd in bookDetailsList do
                let! pub = this.GetPublisherAsync(context, bd, ct)
                itemsWithPublishers.Add(bd, pub)
            let itemsList = List.ofSeq itemsWithPublishers
            let content, contentType =
                match exportFormat with
                | DublinCore -> FormatSerializers.formatDublinCoreBulk itemsList, DublinCore.ContentType
                | MarcXml -> FormatSerializers.formatMarcXmlBulk itemsList, MarcXml.ContentType
                | Mods -> FormatSerializers.formatModsBulk itemsList, Mods.ContentType
                | Onix -> FormatSerializers.formatOnixBulk itemsList, Onix.ContentType
                | BibTex -> FormatSerializers.formatBibTexBulk itemsList, BibTex.ContentType
                | SchemaOrg -> FormatSerializers.formatSchemaOrgGraph itemsList, SchemaOrg.ContentType
                | Csv ->
                    let header = "Id,Title,Isbn,Year,Availability,MainCategory,Authors,Publisher"
                    let rows =
                        itemsList
                        |> List.map (fun (bd, pub) ->
                            let b = bd.Book
                            let authorsStr = bd.Authors |> List.map (fun a -> a.Name.Value) |> String.concat "; "
                            let pubStr = pub |> Option.defaultValue ""
                            sprintf "%s,\"%s\",%s,%d,%s,%s,\"%s\",\"%s\"" (b.BookId.Value.ToString()) (b.Title.Value.Replace("\"", "\"\"")) (b.Isbn.Value) (b.Year.Value) (b.Availability.ToString()) (b.MainCategory.ToString()) (authorsStr.Replace("\"", "\"\"")) (pubStr.Replace("\"", "\"\"")))
                    String.Join("\n", header :: rows), Csv.ContentType
                | Json ->
                    let dtos =
                        itemsList
                        |> List.map (fun (bd, pub) ->
                            let b = bd.Book
                            {|
                                Id = b.BookId.Value
                                Title = b.Title.Value
                                Isbn = b.Isbn.Value
                                SbnCode = b.SbnCode |> Option.map (fun s -> s.Value)
                                Year = b.Year.Value
                                Authors = bd.Authors |> List.map (fun a -> a.Name.Value)
                                Publisher = pub
                                Availability = b.Availability.ToString()
                                MainCategory = b.MainCategory.ToString()
                                Description = b.Description
                                ImageUrl = b.ImageUrl |> Option.map (fun u -> u.ToString())
                                Tags = b.Tags |> List.map (fun t -> t.TagName)
                            |})
                    System.Text.Json.JsonSerializer.Serialize(dtos, System.Text.Json.JsonSerializerOptions(WriteIndented = true)), Json.ContentType
            return this.Content(content, contentType) :> IActionResult
        }

    member private this.ExecuteQuery(tenantParam: string, queryAction: UserContext * CancellationToken -> Task<IActionResult>) =
        task {
            match this.TryResolveTenant tenantParam with
            | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
            | Ok tenantId ->
                let ct = this.HttpContext.RequestAborted
                let! authCheck = this.ValidateRequestApiKey(tenantId, ct)
                match authCheck with
                | Error authResult -> return authResult
                | Ok isApiKeyAuthenticated ->
                    let rawContext = UserContextMapper.mapFromRequest this.Request
                    let context =
                        if isApiKeyAuthenticated && not rawContext.IsAuthenticated then
                            UserContext.Authenticated(UserId tenantId.Value, [ Role.Admin ]).WithNewTenant(tenantId)
                        else
                            rawContext.WithNewTenant(tenantId)
                    let! accessCheck = this.EnsureTenantAccessible(context, tenantId, isApiKeyAuthenticated, ct)
                    match accessCheck with
                    | Error msg -> return this.StatusCode(403, {| message = msg |}) :> IActionResult
                    | Ok () -> return! queryAction (context, ct)
        }

    member private this.GetBookByIsbnInternal(tenantParam: string, isbn: string, formatParam: string) =
        this.ExecuteQuery(tenantParam, fun (context, ct) ->
            task {
                let exportFormat = this.ResolveFormat(formatParam)
                let! booksRes = bookService.SearchByIsbnAsync(context, Isbn isbn, ct = ct)
                match booksRes with
                | Ok [] -> return this.NotFound({| message = $"No book found with ISBN '{isbn}'" |}) :> IActionResult
                | Ok [ singleBook ] ->
                    let! bdRes = detailsService.GetBookDetailsAsync(context, singleBook.BookId, ct)
                    match bdRes with
                    | Ok bd -> return! this.FormatSingleBook(context, bd, exportFormat, ct)
                    | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
                | Ok multipleBooks ->
                    let! detailsList =
                        multipleBooks
                        |> List.traverseTaskResultM (fun b -> detailsService.GetBookDetailsAsync(context, b.BookId, ct))
                    match detailsList with
                    | Ok bds -> return! this.FormatMultipleBooks(context, bds, exportFormat, ct)
                    | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
                | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
            })

    member private this.GetBookBySbnInternal(tenantParam: string, sbn: string, formatParam: string) =
        this.ExecuteQuery(tenantParam, fun (context, ct) ->
            task {
                let exportFormat = this.ResolveFormat(formatParam)
                let! exactBooksRes = bookService.GetAllAsync(context, criteria = SearchCriteria.searchByExactSbnCode sbn, ct = ct)
                let! booksRes =
                    task {
                        match exactBooksRes with
                        | Ok [] -> return! bookService.GetAllAsync(context, criteria = SearchCriteria.searchBySbnCode sbn, ct = ct)
                        | other -> return other
                    }
                match booksRes with
                | Ok [] -> return this.NotFound({| message = $"No book found with SBN '{sbn}'" |}) :> IActionResult
                | Ok [ singleBook ] ->
                    let! bdRes = detailsService.GetBookDetailsAsync(context, singleBook.BookId, ct)
                    match bdRes with
                    | Ok bd -> return! this.FormatSingleBook(context, bd, exportFormat, ct)
                    | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
                | Ok multipleBooks ->
                    let! detailsList =
                        multipleBooks
                        |> List.traverseTaskResultM (fun b -> detailsService.GetBookDetailsAsync(context, b.BookId, ct))
                    match detailsList with
                    | Ok bds -> return! this.FormatMultipleBooks(context, bds, exportFormat, ct)
                    | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
                | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
            })

    member private this.GetBookByIdInternal(tenantParam: string, id: Guid, formatParam: string) =
        this.ExecuteQuery(tenantParam, fun (context, ct) ->
            task {
                let exportFormat = this.ResolveFormat(formatParam)
                let! bookRes = bookService.GetBookAsync(context, BookId id, ct = ct)
                match bookRes with
                | Ok book ->
                    let! bdRes = detailsService.GetBookDetailsAsync(context, book.BookId, ct)
                    match bdRes with
                    | Ok bd -> return! this.FormatSingleBook(context, bd, exportFormat, ct)
                    | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
                | Error msg -> return this.NotFound({| message = msg |}) :> IActionResult
            })

    member private this.GetAllBooksInternal(tenantParam: string, formatParam: string) =
        this.ExecuteQuery(tenantParam, fun (context, ct) ->
            task {
                let exportFormat = this.ResolveFormat(formatParam)
                let! allBooksRes = bookService.GetAllAsync(context, ct = ct)
                match allBooksRes with
                | Ok books ->
                    let! detailsList =
                        books
                        |> List.traverseTaskResultM (fun b -> detailsService.GetBookDetailsAsync(context, b.BookId, ct))
                    match detailsList with
                    | Ok bds -> return! this.FormatMultipleBooks(context, bds, exportFormat, ct)
                    | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
                | Error err -> return this.BadRequest({| message = err |}) :> IActionResult
            })

    // ISBN lookup
    [<HttpGet("books/isbn/{isbn}")>]
    member this.GetBookByIsbnDefault(isbn: string, [<FromQuery>] format: string) =
        this.GetBookByIsbnInternal("default", isbn, format)

    [<HttpGet("{tenant}/books/isbn/{isbn}")>]
    member this.GetBookByIsbnWithTenant(tenant: string, isbn: string, [<FromQuery>] format: string) =
        this.GetBookByIsbnInternal(tenant, isbn, format)

    // SBN lookup
    [<HttpGet("books/sbn/{sbn}")>]
    member this.GetBookBySbnDefault(sbn: string, [<FromQuery>] format: string) =
        this.GetBookBySbnInternal("default", sbn, format)

    [<HttpGet("{tenant}/books/sbn/{sbn}")>]
    member this.GetBookBySbnWithTenant(tenant: string, sbn: string, [<FromQuery>] format: string) =
        this.GetBookBySbnInternal(tenant, sbn, format)

    // ID lookup
    [<HttpGet("books/id/{id}")>]
    member this.GetBookByIdDefault(id: Guid, [<FromQuery>] format: string) =
        this.GetBookByIdInternal("default", id, format)

    [<HttpGet("{tenant}/books/id/{id}")>]
    member this.GetBookByIdWithTenant(tenant: string, id: Guid, [<FromQuery>] format: string) =
        this.GetBookByIdInternal(tenant, id, format)

    // Collection / Bulk lookup
    [<HttpGet("books")>]
    member this.GetAllBooksDefault([<FromQuery>] format: string) =
        this.GetAllBooksInternal("default", format)

    [<HttpGet("{tenant}/books")>]
    member this.GetAllBooksWithTenant(tenant: string, [<FromQuery>] format: string) =
        this.GetAllBooksInternal(tenant, format)

    member private this.ImportBooksInternal
        (
            tenantParam: string,
            formatParam: string,
            preventDuplicates: Nullable<bool>,
            createMissingAuthors: Nullable<bool>,
            createMissingPublishers: Nullable<bool>
        ) =
        this.ExecuteQuery(tenantParam, fun (context, ct) ->
            task {
                if isNull (box catalogImportService) then
                    return this.StatusCode(500, {| message = "Catalog import service is not configured" |}) :> IActionResult
                else
                    let format = this.ResolveImportFormat(formatParam)
                    use reader = new System.IO.StreamReader(this.Request.Body, System.Text.Encoding.UTF8)
                    let! payload = reader.ReadToEndAsync(ct)

                    if String.IsNullOrWhiteSpace payload then
                        return this.BadRequest({| message = "Payload body is empty" |}) :> IActionResult
                    else
                        let maxBatchSize =
                            if not (isNull configuration) then
                                match configuration.GetSection("Catalog:MaxBatchSize").Value with
                                | null -> 250
                                | v -> match Int32.TryParse v with | true, n -> n | _ -> 250
                            else 250

                        let options =
                            {
                                PreventDuplicates = if preventDuplicates.HasValue then preventDuplicates.Value else true
                                CreateMissingAuthors = if createMissingAuthors.HasValue then createMissingAuthors.Value else true
                                CreateMissingPublishers = if createMissingPublishers.HasValue then createMissingPublishers.Value else true
                                MaxBatchSize = Some maxBatchSize
                            }
                        let! importRes = catalogImportService.ImportCatalogAsync(context, payload, format, options, ct)
                        match importRes with
                        | Ok summary ->
                            return this.Ok(summary) :> IActionResult
                        | Error err ->
                            return this.BadRequest({| message = err |}) :> IActionResult
            })

    // Import / Feed books
    [<HttpPost("books")>]
    [<RequestSizeLimit(4194304L)>]
    [<EnableRateLimiting("CatalogIngestionPolicy")>]
    member this.ImportBooksDefault
        (
            [<FromQuery>] format: string,
            [<FromQuery>] preventDuplicates: Nullable<bool>,
            [<FromQuery>] createMissingAuthors: Nullable<bool>,
            [<FromQuery>] createMissingPublishers: Nullable<bool>
        ) =
        this.ImportBooksInternal("default", format, preventDuplicates, createMissingAuthors, createMissingPublishers)

    [<HttpPost("{tenant}/books")>]
    [<RequestSizeLimit(4194304L)>]
    [<EnableRateLimiting("CatalogIngestionPolicy")>]
    member this.ImportBooksWithTenant
        (
            tenant: string,
            [<FromQuery>] format: string,
            [<FromQuery>] preventDuplicates: Nullable<bool>,
            [<FromQuery>] createMissingAuthors: Nullable<bool>,
            [<FromQuery>] createMissingPublishers: Nullable<bool>
        ) =
        this.ImportBooksInternal(tenant, format, preventDuplicates, createMissingAuthors, createMissingPublishers)
