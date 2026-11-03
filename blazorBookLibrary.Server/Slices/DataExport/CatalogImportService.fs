namespace BookLibrary.Services

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open Microsoft.Extensions.DependencyInjection
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Formats
open BookLibrary.Shared.Services

type CatalogImportService
    [<ActivatorUtilitiesConstructor>]
    (
        bookService: IBookService,
        authorService: IAuthorService,
        editorService: IEditorService,
        authorsSearchService: IAuthorsSearchService,
        googleBooksService: IBooksMetadataSearchService,
        textEmbeddingService: ITextEmbeddingService,
        vectorDbService: IVectorDbService,
        [<Optional; DefaultParameterValue(null: obj)>] notificationService: INotificationService
    ) =

    new(bookService: IBookService, authorService: IAuthorService, editorService: IEditorService) =
        CatalogImportService(
            bookService,
            authorService,
            editorService,
            Unchecked.defaultof<IAuthorsSearchService>,
            Unchecked.defaultof<IBooksMetadataSearchService>,
            Unchecked.defaultof<ITextEmbeddingService>,
            Unchecked.defaultof<IVectorDbService>,
            null
        )

    interface ICatalogImportService with
        member this.ImportCatalogAsync(context, payload, format, ?options, ?progress, ?ct) =
            let ct = defaultArg ct CancellationToken.None
            let options = defaultArg options CatalogImportOptions.Default
            let progressReporter = progress |> Option.bind Option.ofObj

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
                    let total = drafts.Length
                    let startTime = DateTime.UtcNow
                    let results = ResizeArray<CatalogImportStatus>()

                    let embeddingGenerationHalted = ref false
                    let descriptionGenerationHalted = ref false
                    let embeddingsCreatedCount = ref 0

                    let reportProgress (processedIndex: int) (label: string option) =
                        match progressReporter with
                        | Some p ->
                            let elapsed = DateTime.UtcNow - startTime
                            let remainingTime =
                                if processedIndex > 0 then
                                    let avgTicksPerItem = elapsed.Ticks / int64 processedIndex
                                    let remainingItems = total - processedIndex
                                    Some (TimeSpan.FromTicks(avgTicksPerItem * int64 remainingItems))
                                else
                                    None
                            p.Report({
                                Current = processedIndex
                                Total = total
                                EstimatedRemainingTime = remainingTime
                                CurrentItemLabel = label
                            })
                        | None -> ()

                    for i in 0 .. total - 1 do
                        if ct.IsCancellationRequested then
                            let draft = drafts.[i]
                            results.Add(CatalogImportInterrupted (if String.IsNullOrWhiteSpace draft.Title then None else Some draft.Title))
                        else
                            let draft = drafts.[i]
                            let labelName =
                                if String.IsNullOrWhiteSpace draft.Title then
                                    defaultArg draft.Isbn "record"
                                else draft.Title
                            reportProgress i (Some $"Processing {labelName}...")

                            let! status =
                                task {
                                    try
                                        let titleStr = draft.Title.Trim()
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
                                            return SkippedDuplicate (isbn.Value, if String.IsNullOrWhiteSpace titleStr then isbn.Value else titleStr)
                                        else
                                            // Optional metadata enrichment via Google Books / ISBN
                                            let mutable currentTitle = titleStr
                                            let mutable currentAuthors = draft.Authors
                                            let mutable currentDescription = draft.Description
                                            let mutable currentImageUrl = draft.ImageUrl
                                            let mutable currentCategories = draft.Categories
                                            let mutable currentYear = draft.Year

                                            if options.EnrichMissingMetadataViaIsbn && not (isNull (box googleBooksService)) && isbn.IsValidIsbn then
                                                try
                                                    let! metaRes = googleBooksService.LookupByIsbnAsync(context, isbn.Value, ct = ct)
                                                    match metaRes with
                                                    | Ok (Some meta) ->
                                                        if String.IsNullOrWhiteSpace currentTitle then
                                                            currentTitle <- meta.Title
                                                        if currentAuthors.IsEmpty && not (isNull meta.Authors) then
                                                            currentAuthors <- List.ofSeq meta.Authors
                                                        if currentDescription.IsNone && meta.Description.IsSome then
                                                            currentDescription <- meta.Description
                                                        if currentCategories.IsEmpty && not (isNull meta.Categories) then
                                                            currentCategories <- List.ofSeq meta.Categories
                                                        if currentYear.IsNone && meta.Year.IsSome then
                                                            currentYear <- meta.Year

                                                        if currentImageUrl.IsNone then
                                                            let! coverRes = googleBooksService.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(context, isbn, ct = ct)
                                                            match coverRes with
                                                            | Ok (Some c) -> currentImageUrl <- Some c
                                                            | _ -> ()
                                                    | _ -> ()
                                                with _ -> ()

                                            if String.IsNullOrWhiteSpace currentTitle && not isbn.IsValidIsbn then
                                                return Failed (None, "Missing or empty book title and no valid ISBN")
                                            else
                                                let title = Title.New (if String.IsNullOrWhiteSpace currentTitle then isbn.Value else currentTitle)

                                                // 1. Resolve or create authors
                                                let! authorResolution =
                                                    task {
                                                        let authorIds = ResizeArray<AuthorId>()
                                                        let mutable errorMsgOpt = None

                                                        for authorName in currentAuthors do
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
                                                                            if options.EnrichAuthorsWithIsniAndPhoto && not (isNull (box authorsSearchService)) then
                                                                                let! authorMeta =
                                                                                    authorsSearchService.LookupByNameAsync(context, trimmed, ct = ct)
                                                                                    |> Task.map (function Ok m -> Some m | Error _ -> None)

                                                                                let! authorPic =
                                                                                    authorsSearchService.LookupImageUrlByNameAndThumbSizeAsync(context, trimmed, ct = ct)
                                                                                    |> Task.map (function
                                                                                        | Ok s when not (String.IsNullOrEmpty s) ->
                                                                                            match Uri.TryCreate(s, UriKind.Absolute) with
                                                                                            | true, uri -> Some uri
                                                                                            | _ -> None
                                                                                        | _ -> None)

                                                                                let isni =
                                                                                    authorMeta
                                                                                    |> Option.bind (fun m -> m.Isni)
                                                                                    |> Option.bind (fun s -> match Isni.New s with | Ok i -> Some i | _ -> None)
                                                                                    |> Option.defaultValue Isni.EmptyIsni

                                                                                let newAuthor =
                                                                                    Author.NewWithOptionalIsniAndImageUrl(
                                                                                        tenantId,
                                                                                        name,
                                                                                        isni,
                                                                                        ?imageUrl = authorPic
                                                                                    )
                                                                                let! addRes = authorService.AddAuthorAsync(context, newAuthor, ct = ct)
                                                                                match addRes with
                                                                                | Ok () -> authorIds.Add newAuthor.AuthorId
                                                                                | Error err -> errorMsgOpt <- Some $"Failed to create author '{trimmed}': {err}"
                                                                            else
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
                                                            currentCategories
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
                                                            currentYear |> Option.defaultValue 1 |> Year.New

                                                        let sbnCode =
                                                            draft.SbnCode
                                                            |> Option.bind (fun s ->
                                                                match SbnCode.New s with
                                                                | Ok valid -> Some valid
                                                                | Error _ -> Some (SbnCode.NewInvalid s))

                                                        let imageUrl =
                                                            currentImageUrl
                                                            |> Option.bind (fun u ->
                                                                match Uri.TryCreate(u, UriKind.Absolute) with
                                                                | true, uri -> Some uri
                                                                | false, _ -> None)

                                                        // 4. Missing Description Generation via AI
                                                        let! finalDescription =
                                                            task {
                                                                match currentDescription with
                                                                | Some d when not (String.IsNullOrWhiteSpace d) ->
                                                                    return Some d
                                                                | _ when options.GenerateMissingDescriptions && not !descriptionGenerationHalted && not (isNull (box textEmbeddingService)) ->
                                                                    reportProgress i (Some $"Generating AI description for '{title.Value}'...")
                                                                    let bookMatch = All(isbn.Value, title.Value, currentAuthors)
                                                                    let! genResult = textEmbeddingService.GetBookDescriptionAsync(context, bookMatch, ct)
                                                                    match genResult with
                                                                    | Ok desc -> return Some desc
                                                                    | Error e when (e.Contains("429") || e.Contains("quota") || e.Contains("limit") || e.Contains("503")) ->
                                                                        descriptionGenerationHalted := true
                                                                        return None
                                                                    | Error _ -> return None
                                                                | _ ->
                                                                    return None
                                                            }

                                                        let bookId = BookId.New()
                                                        let book: Book =
                                                            {
                                                                TenantId = tenantId
                                                                BookId = bookId
                                                                Title = title
                                                                Papers = []
                                                                ImageUrl = imageUrl
                                                                Description = finalDescription
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

                                                        // 5. Vector Embedding Generation
                                                        let! finalBook =
                                                            task {
                                                                if options.GenerateEmbeddings && not !embeddingGenerationHalted && not (isNull (box textEmbeddingService)) && not (isNull (box vectorDbService)) then
                                                                    reportProgress i (Some $"Generating embedding for '{title.Value}'...")
                                                                    let textToEmbed =
                                                                        match book.Description with
                                                                        | Some d when not (String.IsNullOrWhiteSpace d) -> $"{title.Value} {d}"
                                                                        | _ -> title.Value

                                                                    let rec getEmbeddingWithRetry (text: string) (retries: int) =
                                                                        task {
                                                                            let! res = textEmbeddingService.GetEmbeddingAsync(context, text, ct = ct)
                                                                            match res with
                                                                            | Error e when (e.Contains("429") || e.Contains("quota") || e.Contains("limit") || e.Contains("503")) && retries > 0 ->
                                                                                do! Task.Delay(1000, ct)
                                                                                return! getEmbeddingWithRetry text (retries - 1)
                                                                            | _ -> return res
                                                                        }

                                                                    let! embeddingRes = getEmbeddingWithRetry textToEmbed 1
                                                                    match embeddingRes with
                                                                    | Ok embeddingResult ->
                                                                        let embeddingId = EmbeddingDataId.New()
                                                                        let! storeRes = vectorDbService.StoreEmbeddingAsync(embeddingId, tenantId, book.BookId, embeddingResult, ct)
                                                                        match storeRes with
                                                                        | Ok () ->
                                                                            embeddingsCreatedCount := !embeddingsCreatedCount + 1
                                                                            return { book with OptionalEmbedding = Some embeddingId }
                                                                        | Error _ -> return book
                                                                    | Error e when (e.Contains("429") || e.Contains("quota") || e.Contains("limit") || e.Contains("503")) ->
                                                                        embeddingGenerationHalted := true
                                                                        return book
                                                                    | Error _ -> return book
                                                                else
                                                                    return book
                                                            }

                                                        let! addRes = bookService.AddBookAsync(context, finalBook, ct = ct)
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
                    let interruptedCount = itemsList |> List.filter (function CatalogImportInterrupted _ -> true | _ -> false) |> List.length

                    reportProgress total (Some "Import completed")

                    // Dispatch notification if notificationService and userId available
                    let notifServiceOpt =
                        if isNull (box notificationService) then None else Some notificationService

                    match notifServiceOpt, context.UserId with
                    | Some notifService, Some userId ->
                        try
                            let notifTitle =
                                if interruptedCount > 0 then "Book Import Interrupted"
                                elif !embeddingGenerationHalted then "Book Import Finished (Embeddings Paused)"
                                else "Book Import Finished"
                            let notifContent =
                                if interruptedCount > 0 then
                                    $"Import was cancelled/interrupted. {importedCount} books were imported."
                                elif !embeddingGenerationHalted then
                                    $"Import completed: {importedCount} books imported. Vector embeddings generation was paused after {!embeddingsCreatedCount} embeddings due to rate limits."
                                elif options.GenerateEmbeddings then
                                    $"Import completed: {importedCount} books imported with {!embeddingsCreatedCount} vector embeddings generated."
                                else
                                    $"Import completed: {importedCount} books imported."
                            let notification = Notification.New(userId, notifTitle, notifContent, "/books-manager")
                            let! _ = notifService.CreateNotificationAsync(context, notification, ct = ct)
                            ()
                        with _ -> ()
                    | _ -> ()

                    return Ok {
                        TotalProcessed = itemsList.Length
                        ImportedCount = importedCount
                        SkippedCount = skippedCount
                        FailedCount = failedCount
                        InterruptedCount = interruptedCount
                        Items = itemsList
                    }
            }
