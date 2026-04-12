
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
open Sharpino.StateView

open BookLibrary.Domain
open BookLibrary.Details
open FsToolkit.ErrorHandling
open System.Threading.Tasks

open BookLibrary.Shared.Details
open BookLibrary.Details.Details

open BookLibrary.Shared.Services
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Details
open Microsoft.Extensions.Configuration

type AuthorService 
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>
    ) =
    new (eventStore: IEventStore<string>) =
        let messageSenders = MessageSenders.NoSender
        let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore
        let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> eventStore
        let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> eventStore
        let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> eventStore
        let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> eventStore
        AuthorService (
            eventStore,
            messageSenders,
            bookViewerAsync,
            authorViewerAsync,
            editorViewerAsync,
            reservationViewerAsync,
            loanViewerAsync
        )
    new (configuration: IConfiguration)
        =   
        let connectionString = configuration.GetConnectionString("BookLibraryDbConnection")
        let eventStore = PgStorage.PgEventStore connectionString
        AuthorService(eventStore)

    member this.AddAuthorAsync(author: Author, ?ct: CancellationToken) = 
        taskResult
            {
                return!
                    runInitAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    author
                    ct
            }

    member this.AddAuthorsAsync(authors: list<Author>, ?ct: CancellationToken) = 
        taskResult
            {
                return!
                    runMultipleInitAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    (authors |> Array.ofList)
                    ct
            }

    member this.GetAuthorAsync (authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                return! authorViewerAsync ct authorId.Value |> TaskResult.map snd
            }

    member private
        this.GetRefreshableAuthorDetailsAsync(id: AuthorId, ?ct: CancellationToken) =
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let refresher =
                        fun () ->
                            result {
                                let! author = 
                                    authorViewerAsync ct id.Value |> TaskResult.map snd
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                let! books = 
                                    author.Books
                                    |> List.traverseTaskResultM (fun bookId -> bookViewerAsync ct bookId.Value |> TaskResult.map snd)
                                    |> Async.AwaitTask
                                    |> Async.RunSynchronously
                                return
                                    {
                                        Author = author
                                        Books = books
                                    }
                            }
                    result {
                        let! authorDetails = refresher()
                        return
                            {
                                AuthorDetails = authorDetails
                                Refresher = refresher
                            } :> Refreshable<RefreshableAuthorDetails>
                            ,
                            id.Value :: (authorDetails.Author.Books |> List.map _.Value)
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> id.Value
            task
                {
                    return StateView.getRefreshableDetailsAsync<RefreshableAuthorDetails> (fun ct -> detailsBuilder ct) key ct
                }
    member this.GetAuthorDetailsAsync (authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! refreshableAuthorDetails = this.GetRefreshableAuthorDetailsAsync(authorId, ct)
                return refreshableAuthorDetails.AuthorDetails
            }

    member this.GetAuthorsAsync(ids: List<AuthorId>, ?ct: CancellationToken) =
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let authors =
                    ids
                    |> List.traverseTaskResultM (fun id -> this.GetAuthorAsync(id, ct))
                return! authors
            }

    member this.RenameAsync (authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let reamecommand = AuthorCommand.Rename (newName, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        ""
                        reamecommand
                        ct
                return! result
            }

    member this.UpdateIsniAsync (authorId: AuthorId, isni: Isni, ?ct: CancellationToken) = 
        taskResult
            {
                let updateIsniCommand = AuthorCommand.UpdateIsni (isni, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        ""
                        updateIsniCommand
                        ct
                return! result
            }

    member this.UpdateImageUrlAsync (authorId: AuthorId, imageUrl: Uri, ?ct: CancellationToken) = 
        taskResult
            {
                let updateImageUrlCommand = AuthorCommand.UpdateImageUrl (imageUrl, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        ""
                        updateImageUrlCommand
                        ct
                return! result
            }

    member this.RemoveImageUrlAsync (authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let removeImageUrlCommand = AuthorCommand.RemoveImageUrl (DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        ""
                        removeImageUrlCommand
                        ct
                return! result
            }

    member this.SealAsync (authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let sealCommand = AuthorCommand.Seal (DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        ""
                        sealCommand
                        ct
                return! result
            }

    member this.UnsealAsync (authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let unsealCommand = AuthorCommand.Unseal (DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        ""
                        unsealCommand
                        ct
                return! result
            }

    member this.RemoveAuthorAsync(authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let! author = authorViewerAsync ct authorId.Value |> TaskResult.map snd
                return!
                    runDeleteAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    authorId.Value
                    (fun _ -> author.Books.Length = 0)
                    ct
            }

    member this.GetAllAuthorsAsync(?ct: CancellationToken) = 
        taskResult
            {
                let! authorsWithId = getAllAggregateStatesAsync<Author, AuthorEvent, string> eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }

    member this.GetAllAuthorsFilteredByName(name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let filter (author: Author) = author.Name.Value.Contains(name.Value, StringComparison.OrdinalIgnoreCase)
                let! authorsWithId = getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }


    member this.GetAllAuthorsFilteredByIsni(isni: Isni, ?ct: CancellationToken) = 
        taskResult
            {
                let filter (author: Author) = author.Isni.Value.Contains(isni.Value, StringComparison.OrdinalIgnoreCase)
                let! authorsWithId = getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }

    member this.GetAllAuthorsFilteredByIsniAndName(isni: Isni, name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let filter (author: Author) = 
                    author.Isni.Value.Contains(isni.Value, StringComparison.OrdinalIgnoreCase) || 
                    author.Name.Value.Contains(name.Value, StringComparison.OrdinalIgnoreCase)
                let! authorsWithId = getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }
                    
    interface IAuthorService with
        member this.AddAuthorAsync(author: Author, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.AddAuthorAsync(author, ct)
        member this.AddAuthorsAsync(authors: list<Author>, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.AddAuthorsAsync(authors, ct)
        member this.GetAuthorAsync (authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorAsync(authorId, ct)
        member this.GetAuthorDetailsAsync (authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorDetailsAsync(authorId, ct)
        member this.GetAuthorsAsync(ids: List<AuthorId>, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorsAsync(ids, ct)
        member this.RenameAsync (authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RenameAsync(authorId, newName, ct)
        member this.RemoveAsync (authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveAuthorAsync(authorId, ct)
        member this.UpdateIsniAsync(authorId: AuthorId, isni: Isni, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateIsniAsync(authorId, isni, ct)
        member this.UpdateImageUrlAsync(authorId: AuthorId, imageUrl: Uri, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateImageUrlAsync(authorId, imageUrl, ct)
        member this.RemoveImageUrlAsync(authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveImageUrlAsync(authorId, ct)
        member this.SealAsync(authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.SealAsync(authorId, ct)
        member this.UnsealAsync(authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UnsealAsync(authorId, ct)
        member this.GetAllAsync(?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsAsync(ct)
        member this.SearchByNameAsync(name: Name, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByName(name, ct)
        member this.SearchByIsniAsync(isni: Isni, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByIsni(isni, ct)
        member this.SearchByIsniAndNameAsync(isni: Isni, name: Name, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByIsniAndName(isni, name, ct)

