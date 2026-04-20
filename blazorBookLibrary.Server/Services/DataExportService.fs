
namespace BookLibrary.Services
open System.Threading
open System
open Sharpino
open Sharpino.Cache
open FSharpPlus.Operators
open Sharpino.CommandHandler
open Sharpino.EventBroker
open Sharpino.Definitions
open Sharpino.Core
open Sharpino.EventBroker
open Sharpino.Storage
open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks
open BookLibrary.Domain
open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open Microsoft.AspNetCore.Identity
open blazorBookLibrary.Data
open BookLibrary.Utils
open Microsoft.Extensions.DependencyInjection
open FsToolkit.ErrorHandling

type DataExportService
    (
            eventStore: IEventStore<string>,
            messageSenders: MessageSenders,
            bookViewerAsync: AggregateViewerAsync2<Book>,
            authorViewerAsync: AggregateViewerAsync2<Author>,
            editorViewerAsync: AggregateViewerAsync2<Editor>,
            reservationViewerAsync: AggregateViewerAsync2<Reservation>,
            loanViewerAsync: AggregateViewerAsync2<Loan>,
            userViewerAsync: AggregateViewerAsync2<User>,
            bookService: IBookService,
            authorService: IAuthorService,
            detailsService: IDetailsService,
            googleBooksService: IGoogleBooksService,
            authorsSearchService: IAuthorsSearchService  
    ) =

    new (
        secretsReader: SecretsReader, 
        bookService: IBookService, 
        authorService: IAuthorService,
        detailsService: IDetailsService, 
        googleBooksService: IGoogleBooksService,
        authorsSearchService: IAuthorsSearchService
        ) =
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> eventStore
        let reviewViewerAsync = getAggregateStorageFreshStateViewerAsync<Review, ReviewEvent, string> eventStore

        DataExportService(
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync,
            userViewerAsync,
            bookService,
            authorService,
            detailsService,
            googleBooksService,
            authorsSearchService
        )
    
    interface IDataExportService with
        member this.ExportAllBooksAsync(exportFormat, ?ct) =
            taskResult {
                let ct = defaultArg ct CancellationToken.None
                let! allBooks = bookService.GetAllAsync(ct = ct) 
                let bookIds = allBooks |> List.map _.BookId
                let! bookDetails = 
                    bookIds
                    |> List.traverseTaskResultM (fun bookId -> detailsService.GetBookDetailsAsync(bookId, ct))
                match exportFormat with
                | Json ->
                    let json = System.Text.Json.JsonSerializer.Serialize(allBooks, jsonOptions)
                    return json
                | Csv ->
                    let header = "Id,Title,Isbn,Year,Availability,MainCategory"
                    let rows = 
                        bookDetails
                        |> List.map (fun bd -> 
                            let b = bd.Book
                            sprintf "%s,\"%s\",%s,%d,%s,%s" 
                                (b.BookId.Value.ToString())
                                (b.Title.Value.Replace("\"", "\"\""))
                                (b.Isbn.Value)
                                (b.Year.Value)
                                (b.Availability.ToString())
                                (b.MainCategory.ToString())
                        )
                    let csv = String.Join("\n", header :: rows)
                    return csv
            }

        member this.ImportFromIsbns (isbns: List<Isbn>, preventDuplicates: bool, generateUnknownAuthors: bool, progress: IProgress<ImportProgress>, ct: CancellationToken) =
            task {
                let progressReporter = Option.ofObj progress
                let mutable success = 0
                let mutable failure = 0
                let total = List.length isbns
                let startTime = DateTime.UtcNow
                
                for i in 0 .. isbns.Length - 1 do
                    let isbn = isbns.[i]
                    do! Task.Delay(1000)
                    
                    let! bookImportResult = taskResult {
                        let! skip = 
                            if preventDuplicates then
                                task {
                                    let! existingResult = bookService.SearchByIsbnAsync(isbn, ct = ct)
                                    match existingResult with
                                    | Ok l -> return Ok (not (List.isEmpty l))
                                    | Error e -> return Error e
                                }
                            else Task.FromResult (Ok false)
                        
                        if skip then
                            return! Error "Duplicate"
                        else
                            let rec lookupWithRetry (isbn: string) (retries: int) =
                                task {
                                    let! res = googleBooksService.LookupByIsbnAsync(isbn)
                                    match res with
                                    | Error e when e.Contains("503") && retries > 0 ->
                                        do! Task.Delay(5000)
                                        return! lookupWithRetry isbn (retries - 1)
                                    | _ -> return res
                                }

                            let! (metadataOpt: GoogleBookMetadata option) = lookupWithRetry isbn.Value 3
                            
                            match metadataOpt with
                            | Some metadata ->
                                let! (coverImageOpt: string option) = 
                                    googleBooksService.LookupCoverImageByIsbnWithOpenApiAndThenGoogleAsync(isbn)
                                    |> Task.map (fun r -> match r with | Ok s -> Ok s | _ -> Ok None)
                                
                                let imageUrl = 
                                    coverImageOpt
                                    |> Option.bind (fun s -> 
                                        match Uri.TryCreate(s, UriKind.Absolute) with
                                        | true, uri -> Some uri
                                        | _ -> None)
                                
                                let authorsToProcess = 
                                    Option.ofObj metadata.Authors 
                                    |> Option.map List.ofSeq 
                                    |> Option.defaultValue []
                                
                                let! authorIds = 
                                    authorsToProcess
                                    |> List.traverseTaskResultM (fun authorName ->
                                        taskResult {
                                            let name = Name.New authorName
                                            let! localAuthors = authorService.SearchByNameAsync(name, ct = ct)
                                            if not (List.isEmpty localAuthors) then
                                                return Some localAuthors.[0].AuthorId
                                            elif generateUnknownAuthors then
                                                let! authorMeta = 
                                                    authorsSearchService.LookupByNameAsync authorName
                                                    |> Task.map (fun r -> 
                                                        match r with
                                                        | Ok m -> Ok (Some m)
                                                        | Error _ -> Ok None)
                                                
                                                let! authorPic = 
                                                    authorsSearchService.LookupImageUrlByNameAndThumbSizeAsync authorName
                                                    |> Task.map (fun r -> 
                                                        match r with
                                                        | Ok s when not (String.IsNullOrEmpty s) -> 
                                                            match Uri.TryCreate(s, UriKind.Absolute) with
                                                            | true, uri -> Ok (Some uri)
                                                            | _ -> Ok None
                                                        | _ -> Ok None)
                                                
                                                let isni = 
                                                    authorMeta 
                                                    |> Option.bind (fun m -> m.Isni)
                                                    |> Option.bind (fun s -> match Isni.New s with | Ok i -> Some i | _ -> None) 
                                                    |> Option.defaultValue Isni.EmptyIsni
                                                
                                                let author = Author.NewWithOptionalIsniAndImageUrl(name, isni, ?imageUrl = authorPic)
                                                let! _ = authorService.AddAuthorAsync(author, ct = ct)
                                                return Some author.AuthorId
                                            else
                                                return None
                                        }
                                    )
                                
                                let finalAuthorIds = authorIds |> List.choose id
                                
                                let matchedCategories = 
                                    Option.ofObj metadata.Categories
                                    |> Option.map List.ofSeq
                                    |> Option.defaultValue []
                                    |> List.map Category.New
                                    |> List.filter (fun c -> c <> Category.Other)
                                
                                let mainCategory = 
                                    matchedCategories 
                                    |> List.tryHead 
                                    |> Option.defaultValue Category.Other
                                
                                let additionalCategories = 
                                    if List.isEmpty matchedCategories then []
                                    else matchedCategories |> List.skip 1
                                
                                let year = metadata.Year |> Option.defaultValue 1 |> Year.New
                                
                                let book = 
                                    {
                                        BookId = BookId.New()
                                        Title = Title.New metadata.Title
                                        ImageUrl = imageUrl
                                        Description = metadata.Description
                                        Availability = Availability.Circulating
                                        Authors = finalAuthorIds
                                        Translators = []
                                        Languages = []
                                        CurrentReservations = []
                                        CurrentLoan = None
                                        Editor = None
                                        MainCategory = mainCategory
                                        AdditionalCategories = additionalCategories
                                        Year = year
                                        Isbn = isbn
                                        Sealed = Sealed.New(DateTime.UtcNow)
                                    }
                                let! _ = bookService.AddBookAsync(book, ct = ct)
                                return ()
                            | None -> 
                                return! Error "Metadata not found"
                    }
                    
                    match bookImportResult with
                    | Ok _ -> success <- success + 1
                    | Error _ -> failure <- failure + 1
                    
                    let processed = i + 1
                    match progressReporter with
                    | Some p ->
                        let elapsed = DateTime.UtcNow - startTime
                        let avgTicksPerItem = elapsed.Ticks / int64 processed
                        let remainingItems = total - processed
                        let remainingTicks = avgTicksPerItem * int64 remainingItems
                        let estimatedRemaining = TimeSpan.FromTicks(remainingTicks)
                        p.Report({ Current = processed; Total = total; EstimatedRemainingTime = Some estimatedRemaining })
                    | None -> ()
                
                return Ok (success, failure)
            }
