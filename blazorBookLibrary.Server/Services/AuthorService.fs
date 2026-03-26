
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
    new (connectionString: string)
        =
        let eventStore = PgStorage.PgEventStore connectionString
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
        AuthorService(connectionString)

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
            this.AddAuthorAsync(author, ct |> Option.defaultValue CancellationToken.None)
        member this.AddAuthorsAsync(authors: list<Author>, ?ct: CancellationToken) = 
            this.AddAuthorsAsync(authors, ct |> Option.defaultValue CancellationToken.None)

        member this.GetAuthorAsync (authorId: AuthorId, ?ct: CancellationToken) = 
            this.GetAuthorAsync(authorId, ct |> Option.defaultValue CancellationToken.None)
        member this.RenameAsync (authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
            this.RenameAsync(authorId, newName, ct |> Option.defaultValue CancellationToken.None)        
        member this.UpdateIsniAsync(authorId: AuthorId, isni: Isni, ?ct: CancellationToken) = 
            this.UpdateIsniAsync(authorId, isni, ct |> Option.defaultValue CancellationToken.None)
        member this.SealAsync(authorId: AuthorId, ?ct: CancellationToken) = 
            this.SealAsync(authorId, ct |> Option.defaultValue CancellationToken.None)
        member this.UnsealAsync(authorId: AuthorId, ?ct: CancellationToken) = 
            this.UnsealAsync(authorId, ct |> Option.defaultValue CancellationToken.None)
        member this.GetAllAsync(?ct: CancellationToken) = 
            this.GetAllAuthorsAsync(ct |> Option.defaultValue CancellationToken.None)
        member this.SearchByNameAsync(name: Name, ?ct: CancellationToken) = 
            this.GetAllAuthorsFilteredByName(name, ct |> Option.defaultValue CancellationToken.None)
        member this.SearchByIsniAsync(isni: Isni, ?ct: CancellationToken) = 
            this.GetAllAuthorsFilteredByIsni(isni, ct |> Option.defaultValue CancellationToken.None)
        member this.SearchByIsniAndNameAsync(isni: Isni, name: Name, ?ct: CancellationToken) = 
            this.GetAllAuthorsFilteredByIsniAndName(isni, name, ct |> Option.defaultValue CancellationToken.None)





