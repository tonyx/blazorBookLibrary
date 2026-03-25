
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

    member this.GetAuthorAsync (authorId: AuthorId, ?ct: CancellationToken) = 
        taskResult
            {
                return! authorViewerAsync ct authorId.Value |> TaskResult.map snd
            }

    member this.RenameAsync (authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
        taskResult
            {
                let reamecommand = AuthorCommand.Rename (newName, DateTime.Now)
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
                let updateIsniCommand = AuthorCommand.UpdateIsni (isni, DateTime.Now)
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
                    
    interface IAuthorService with
        member this.AddAuthorAsync(author: Author, ?ct: CancellationToken) = 
            this.AddAuthorAsync(author, ct |> Option.defaultValue CancellationToken.None)
        member this.GetAuthorAsync (authorId: AuthorId, ?ct: CancellationToken) = 
            this.GetAuthorAsync(authorId, ct |> Option.defaultValue CancellationToken.None)
        member this.RenameAsync (authorId: AuthorId, newName: Name, ?ct: CancellationToken) = 
            this.RenameAsync(authorId, newName, ct |> Option.defaultValue CancellationToken.None)        
        member this.UpdateIsniAsync(authorId: AuthorId, isni: Isni, ?ct: CancellationToken) = 
            this.UpdateIsniAsync(authorId, isni, ct |> Option.defaultValue CancellationToken.None)
        

