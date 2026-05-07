
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
open BookLibrary.Utils

type AuthorService 
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        bookViewerAsync: AggregateViewerAsync2<Book>,
        authorViewerAsync: AggregateViewerAsync2<Author>,
        editorViewerAsync: AggregateViewerAsync2<Editor>,
        reservationViewerAsync: AggregateViewerAsync2<Reservation>,
        loanViewerAsync: AggregateViewerAsync2<Loan>,
        secretsReader: SecretsReader
    ) =
    new (eventStore: IEventStore<string>, secretsReader: SecretsReader) =
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
            loanViewerAsync,
            secretsReader
        )
    new (secretsReader: SecretsReader)
        =   
        let connectionString = secretsReader.GetBookLibraryConnectionString ()
        let eventStore = PgStorage.PgEventStore connectionString
        AuthorService(eventStore, secretsReader)

    member this.AddAuthorAsync(context: UserContext, author: Author, ?ct: CancellationToken) = 
        taskResult
            {
                return!
                    runInitAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    author
                    ct
            }

    member this.AddAuthorsAsync(context: UserContext, authors: list<Author>, ?ct: CancellationToken) = 
        taskResult
            {
                return!
                    runMultipleInitAsync<Author, AuthorEvent, string>
                    eventStore
                    messageSenders
                    (authors |> Array.ofList)
                    ct
            }

    member this.GetAuthorAsync (context: UserContext, authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                return! authorViewerAsync ct authorId.Value |> TaskResult.map snd
            }

    member private
        this.GetRefreshableAuthorDetailsAsync(context: UserContext, id: AuthorId, ?ct: CancellationToken) =
            let detailsBuilder =
                fun (ct: Option<CancellationToken>) ->
                    let refresher =
                        fun (ct: Option<CancellationToken>) ->
                            taskResult {
                                let! author = 
                                    authorViewerAsync ct id.Value |> TaskResult.map snd
                                let! books = 
                                    author.Books
                                    |> List.traverseTaskResultM (fun bookId -> bookViewerAsync ct bookId.Value |> TaskResult.map snd)
                                return
                                    {
                                        Author = author
                                        Books = books
                                    }
                            }
                    taskResult {
                        let! authorDetails = refresher ct
                        return
                            {
                                AuthorDetails = authorDetails
                                Refresher = refresher
                            } :> RefreshableAsync<RefreshableAuthorDetails>
                            ,
                            id.Value :: (authorDetails.Author.Books |> List.map _.Value)
                    }
            let key = DetailsCacheKey.OfType typeof<RefreshableAuthorDetails> id.Value
            StateView.getRefreshableDetailsTaskResultAsync<RefreshableAuthorDetails> (fun ct -> detailsBuilder ct) key ct

    member this.GetAuthorDetailsAsync (context: UserContext, authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let! refreshableAuthorDetails = this.GetRefreshableAuthorDetailsAsync(context, authorId, ct)
                return refreshableAuthorDetails.AuthorDetails
            }

    member this.GetAuthorsAsync(context: UserContext, ids: List<AuthorId>, ?ct: CancellationToken) =
        taskResult
            {
                let ct = defaultArg ct CancellationToken.None
                let authors =
                    ids
                    |> List.traverseTaskResultM (fun id -> this.GetAuthorAsync(context, id, ct))
                return! authors
            }

    member this.RenameAsync (context: UserContext, authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let reamecommand = AuthorCommand.Rename (newName, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        reamecommand
                        ct
                return! result
            }

    member this.UpdateIsniAsync (context: UserContext, authorId: AuthorId, isni: Isni, ?ct: CancellationToken) = 
        taskResult
            {
                let updateIsniCommand = AuthorCommand.UpdateIsni (isni, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        updateIsniCommand
                        ct
                return! result
            }

    member this.UpdateBioAsync (context: UserContext, authorId: AuthorId, bio: string, ?ct: CancellationToken) = 
        taskResult
            {
                let updateBioCommand = AuthorCommand.UpdateBio (bio, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        updateBioCommand
                        ct
                return! result
            }

    member this.UpdateWikipediaUriAsync (context: UserContext, authorId: AuthorId, wikipediaUri: Uri, ?ct: CancellationToken) = 
        taskResult
            {
                let updateWikipediaUriCommand = AuthorCommand.UpdateWikipediaUri (wikipediaUri, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        updateWikipediaUriCommand
                        ct
                return! result
            }

    member this.UpdateImageUrlAsync (context: UserContext, authorId: AuthorId, imageUrl: Uri, ?ct: CancellationToken) = 
        taskResult
            {
                let updateImageUrlCommand = AuthorCommand.UpdateImageUrl (imageUrl, DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        updateImageUrlCommand
                        ct
                return! result
            }

    member this.RemoveImageUrlAsync (context: UserContext, authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let removeImageUrlCommand = AuthorCommand.RemoveImageUrl (DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        removeImageUrlCommand
                        ct
                return! result
            }

    member this.SealAsync (context: UserContext, authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let sealCommand = AuthorCommand.Seal (DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        sealCommand
                        ct
                return! result
            }

    member this.UnsealAsync (context: UserContext, authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                let unsealCommand = AuthorCommand.Unseal (DateTime.UtcNow)
                let result = 
                    runAggregateCommandMdAsync<Author, AuthorEvent, string>
                        authorId.Value
                        eventStore
                        messageSenders
                        (context.ToString())
                        unsealCommand
                        ct
                return! result
            }

    member this.RemoveAuthorAsync(context: UserContext, authorId: AuthorId, ?ct: CancellationToken) = 
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

    member this.GetAllAuthorsAsync(context: UserContext, ?ct: CancellationToken) = 
        taskResult
            {
                let! authorsWithId = getAllAggregateStatesAsync<Author, AuthorEvent, string> eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }

    member this.GetAllAuthorsFilteredByName(context: UserContext, name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let filter (author: Author) = author.Name.Value.Contains(name.Value, StringComparison.OrdinalIgnoreCase)
                let! authorsWithId = getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }


    member this.GetAllAuthorsFilteredByIsni(context: UserContext, isni: Isni, ?ct: CancellationToken) = 
        taskResult
            {
                let filter (author: Author) = author.Isni.Value.Contains(isni.Value, StringComparison.OrdinalIgnoreCase)
                let! authorsWithId = getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }

    member this.GetAllAuthorsFilteredByIsniAndName(context: UserContext, isni: Isni, name: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let filter (author: Author) = 
                    author.Isni.Value.Contains(isni.Value, StringComparison.OrdinalIgnoreCase) || 
                    author.Name.Value.Contains(name.Value, StringComparison.OrdinalIgnoreCase)
                let! authorsWithId = getAllFilteredAggregateStatesAsync<Author, AuthorEvent, string> filter eventStore ct 
                return authorsWithId |> List.ofSeq |> List.map snd
            }
                    
    interface IAuthorService with
        member this.AddAuthorAsync(context, author: Author, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.AddAuthorAsync(context, author, ct)
        member this.AddAuthorsAsync(context, authors: list<Author>, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.AddAuthorsAsync(context, authors, ct)
        member this.GetAuthorAsync (context, authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorAsync(context, authorId, ct)
        member this.GetAuthorDetailsAsync (context, authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorDetailsAsync(context, authorId, ct)
        member this.GetAuthorsAsync(context, ids: List<AuthorId>, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAuthorsAsync(context, ids, ct)
        member this.RenameAsync (context, authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RenameAsync(context, authorId, newName, ct)
        member this.RemoveAsync (context, authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveAuthorAsync(context, authorId, ct)
        member this.UpdateIsniAsync(context, authorId: AuthorId, isni: Isni, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateIsniAsync(context, authorId, isni, ct)
        member this.UpdateBioAsync(context, authorId: AuthorId, bio: string, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateBioAsync(context, authorId, bio, ct)
        member this.UpdateWikipediaUriAsync(context, authorId: AuthorId, wikipediaUri: Uri, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateWikipediaUriAsync(context, authorId, wikipediaUri, ct)
        member this.UpdateImageUrlAsync(context, authorId: AuthorId, imageUrl: Uri, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UpdateImageUrlAsync(context, authorId, imageUrl, ct)
        member this.RemoveImageUrlAsync(context, authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.RemoveImageUrlAsync(context, authorId, ct)
        member this.SealAsync(context, authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.SealAsync(context, authorId, ct)
        member this.UnsealAsync(context, authorId: AuthorId, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.UnsealAsync(context, authorId, ct)
        member this.GetAllAsync(context, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsAsync(context, ct)
        member this.SearchByNameAsync(context, name: Name, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByName(context, name, ct)
        member this.SearchByIsniAsync(context, isni: Isni, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByIsni(context, isni, ct)
        member this.SearchByIsniAndNameAsync(context, isni: Isni, name: Name, ?ct: CancellationToken) = 
            let ct = defaultArg ct CancellationToken.None
            this.GetAllAuthorsFilteredByIsniAndName(context, isni, name, ct)

