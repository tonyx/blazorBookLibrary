namespace BookLibrary.Services

open System
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Formats
open BookLibrary.Shared.Services

type CatalogImportService
    (
        bookService: IBookService,
        authorService: IAuthorService,
        editorService: IEditorService
    ) =

    interface ICatalogImportService with
        member this.ImportCatalogAsync(context, payload, format, ?options, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            let options = defaultArg options CatalogImportOptions.Default

            task {
                match FormatParsers.parse format payload with
                | Error parseErr ->
                    return Error parseErr
                | Ok drafts when drafts.IsEmpty ->
                    return Ok CatalogImportResult.Empty
                | Ok drafts when (let maxAllowed = options.MaxBatchSize |> Option.defaultValue 250 in drafts.Length > maxAllowed) ->
                    let maxAllowed = options.MaxBatchSize |> Option.defaultValue 250
                    return Error $"Batch size ({drafts.Length} items) exceeds maximum allowed limit of {maxAllowed} items per request."
                | Ok drafts ->
                    let tenantId = context.TenantId |> Option.defaultValue TenantId.Default
                    let results = ResizeArray<CatalogImportStatus>()

                    for draft in drafts do
                        let! status =
                            task {
                                try
                                    if String.IsNullOrWhiteSpace draft.Title then
                                        return Failed (None, "Missing or empty book title")
                                    else
                                        let title = Title.New (draft.Title.Trim())
                                        let isbnVal = draft.Isbn |> Option.defaultValue ""
                                        let isbn =
                                            if String.IsNullOrWhiteSpace isbnVal then
                                                Isbn.NewEmpty()
                                            else
                                                match Isbn.New isbnVal with
                                                | Ok validIsbn -> validIsbn
                                                | Error _ -> Isbn.NewInvalid isbnVal

                                        // Duplicate check
                                        let! isDuplicate =
                                            task {
                                                if options.PreventDuplicates && isbn.IsValidIsbn then
                                                    let! searchRes = bookService.SearchByIsbnAsync(context, isbn, ct = ct)
                                                    match searchRes with
                                                    | Ok found when not found.IsEmpty -> return true
                                                    | _ -> return false
                                                else
                                                    return false
                                            }

                                        if isDuplicate then
                                            return SkippedDuplicate (isbn.Value, title.Value)
                                        else
                                            // 1. Resolve or create authors
                                            let! authorResolution =
                                                task {
                                                    let authorIds = ResizeArray<AuthorId>()
                                                    let mutable errorMsgOpt = None

                                                    for authorName in draft.Authors do
                                                        if errorMsgOpt.IsNone then
                                                            let trimmed = authorName.Trim()
                                                            let name = Name.New trimmed
                                                            if name.Value = "" then ()
                                                            else
                                                                let! existingRes = authorService.SearchByNameAsync(context, name, ct = ct)
                                                                match existingRes with
                                                                | Ok (existingAuthor :: _) ->
                                                                    authorIds.Add existingAuthor.AuthorId
                                                                | _ ->
                                                                    if options.CreateMissingAuthors then
                                                                        let newAuthor = Author.NewWithoutIsni tenantId name
                                                                        let! addRes = authorService.AddAuthorAsync(context, newAuthor, ct = ct)
                                                                        match addRes with
                                                                        | Ok () -> authorIds.Add newAuthor.AuthorId
                                                                        | Error err -> errorMsgOpt <- Some $"Failed to create author '{trimmed}': {err}"
                                                                    else
                                                                        errorMsgOpt <- Some $"Author '{trimmed}' does not exist and CreateMissingAuthors is false"

                                                    match errorMsgOpt with
                                                    | Some err -> return Error err
                                                    | None -> return Ok (List.ofSeq authorIds)
                                                }

                                            match authorResolution with
                                            | Error authorErr ->
                                                return Failed (Some title.Value, authorErr)
                                            | Ok authorIds ->
                                                // 2. Resolve or create publisher / editor
                                                let! editorResolution =
                                                    task {
                                                        match draft.Publisher with
                                                        | None -> return Ok None
                                                        | Some p when String.IsNullOrWhiteSpace p -> return Ok None
                                                        | Some pubName ->
                                                            let trimmedPub = pubName.Trim()
                                                            let name = Name.New trimmedPub
                                                            if name.Value = "" then return Ok None
                                                            else
                                                                let! allEditorsRes = editorService.GetAllAsync(context, ct = ct)
                                                                match allEditorsRes with
                                                                | Ok editors ->
                                                                    let existingOpt =
                                                                        editors
                                                                        |> List.tryFind (fun ed ->
                                                                            String.Equals(ed.Name.Value, name.Value, StringComparison.OrdinalIgnoreCase))
                                                                    match existingOpt with
                                                                    | Some ed -> return Ok (Some ed.EditorId)
                                                                    | None ->
                                                                        if options.CreateMissingPublishers then
                                                                            let newEditor = Editor.NewWithoutDate tenantId name
                                                                            let! addEdRes = editorService.AddEditorAsync(context, newEditor, ct = ct)
                                                                            match addEdRes with
                                                                            | Ok ed -> return Ok (Some ed.EditorId)
                                                                            | Error err -> return Error $"Failed to create publisher '{trimmedPub}': {err}"
                                                                        else
                                                                            return Ok None
                                                                | Error err ->
                                                                    return Error $"Failed to retrieve editors: {err}"
                                                    }

                                                match editorResolution with
                                                | Error editorErr ->
                                                    return Failed (Some title.Value, editorErr)
                                                | Ok editorIdOpt ->
                                                    // 3. Category & Tags mapping
                                                    let matchedCategories =
                                                        draft.Categories
                                                        |> List.map Category.New
                                                        |> List.filter (fun c -> c <> Category.Other)

                                                    let mainCategory =
                                                        matchedCategories |> List.tryHead |> Option.defaultValue Category.Other

                                                    let additionalCategories =
                                                        if matchedCategories.Length > 1 then
                                                            matchedCategories |> List.skip 1
                                                        else []

                                                    let tagsFromKeywords =
                                                        draft.Tags
                                                        |> List.map (fun t -> BookTag t)

                                                    let year =
                                                        draft.Year |> Option.defaultValue 1 |> Year.New

                                                    let sbnCode =
                                                        draft.SbnCode
                                                        |> Option.bind (fun s ->
                                                            match SbnCode.New s with
                                                            | Ok valid -> Some valid
                                                            | Error _ -> Some (SbnCode.NewInvalid s))

                                                    let imageUrl =
                                                        draft.ImageUrl
                                                        |> Option.bind (fun u ->
                                                            match Uri.TryCreate(u, UriKind.Absolute) with
                                                            | true, uri -> Some uri
                                                            | false, _ -> None)

                                                    let bookId = BookId.New()
                                                    let book: Book =
                                                        {
                                                            TenantId = tenantId
                                                            BookId = bookId
                                                            Title = title
                                                            Papers = []
                                                            ImageUrl = imageUrl
                                                            Description = draft.Description
                                                            OptionalEmbedding = None
                                                            Availability = Availability.Circulating
                                                            DistributionPoint = None
                                                            Authors = authorIds
                                                            Translators = []
                                                            Languages = []
                                                            CurrentLoan = None
                                                            Editor = editorIdOpt
                                                            MainCategory = mainCategory
                                                            AdditionalCategories = additionalCategories
                                                            Tags = tagsFromKeywords
                                                            Year = year
                                                            Isbn = isbn
                                                            SbnCode = sbnCode
                                                            Sealed = Sealed.New(DateTime.UtcNow)
                                                        }

                                                    let! addRes = bookService.AddBookAsync(context, book, ct = ct)
                                                    match addRes with
                                                    | Ok () -> return Imported (bookId.Value, title.Value)
                                                    | Error err -> return Failed (Some title.Value, err)
                                with ex ->
                                    return Failed (Some draft.Title, ex.Message)
                            }
                        results.Add status

                    let itemsList = List.ofSeq results
                    let importedCount = itemsList |> List.filter (function Imported _ -> true | _ -> false) |> List.length
                    let skippedCount = itemsList |> List.filter (function SkippedDuplicate _ -> true | _ -> false) |> List.length
                    let failedCount = itemsList |> List.filter (function Failed _ -> true | _ -> false) |> List.length

                    return Ok {
                        TotalProcessed = itemsList.Length
                        ImportedCount = importedCount
                        SkippedCount = skippedCount
                        FailedCount = failedCount
                        Items = itemsList
                    }
            }
