
module TestSetup

open System
open DotNetEnv
open Sharpino.PgStorage
open BookLibrary.Domain
open BookLibrary.Services
open BookLibrary.Shared.Details
open Sharpino.Cache
open Sharpino.Core
open BookLibrary.Shared.Commons
open Sharpino.CommandHandler
open Sharpino.EventBroker
open BookLibrary.Details.Details
open Microsoft.Extensions.Configuration
open System.Threading

Env.Load() |> ignore
let password = Environment.GetEnvironmentVariable("password")

let connection =
    "Server=127.0.0.1;"+
    "Database=sharpino_booklibrary_test;" +
    "User Id=safe;"+
    $"Password={password}"

let pgEventStore:Sharpino.Storage.IEventStore<string> = PgEventStore connection

let setUp () =
    pgEventStore.Reset Book.Version Book.StorageName
    pgEventStore.ResetAggregateStream Book.Version Book.StorageName
    pgEventStore.Reset Author.Version Author.StorageName
    pgEventStore.ResetAggregateStream Author.Version Author.StorageName
    pgEventStore.Reset Editor.Version Editor.StorageName
    pgEventStore.ResetAggregateStream Editor.Version Editor.StorageName
    pgEventStore.Reset Reservation.Version Reservation.StorageName
    pgEventStore.ResetAggregateStream Reservation.Version Reservation.StorageName
    pgEventStore.Reset Loan.Version Loan.StorageName
    pgEventStore.ResetAggregateStream Loan.Version Loan.StorageName
    pgEventStore.Reset User.Version User.StorageName
    pgEventStore.ResetAggregateStream User.Version User.StorageName
    AggregateCache3.Instance.Clear()            

let timeSlotDurationInDays =
    let config = 
        ConfigurationBuilder()
            .AddJsonFile("appSettings.json", true)
            .Build()

    config.GetValue<int>("BookLibrary::TimeSlotLoanDurationInDays", 30)

let bookViewerAsync = getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> pgEventStore
let authorViewerAsync = getAggregateStorageFreshStateViewerAsync<Author, AuthorEvent, string> pgEventStore
let editorViewerAsync = getAggregateStorageFreshStateViewerAsync<Editor, EditorEvent, string> pgEventStore
let reservationViewerAsync = getAggregateStorageFreshStateViewerAsync<Reservation, ReservationEvent, string> pgEventStore
let loanViewerAsync = getAggregateStorageFreshStateViewerAsync<Loan, LoanEvent, string> pgEventStore
let userViewerAsync = getAggregateStorageFreshStateViewerAsync<User, UserEvent, string> pgEventStore

let getAuthorService () = 
    AuthorService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync)

let getReservationService () =
    ReservationService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync)

let getBookService () = 
    BookService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync,
        getReservationService())


let getLoanService () =
    LoanService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync)

let getUserService () =
    UserService(
        pgEventStore, 
        MessageSenders.NoSender, 
        bookViewerAsync, 
        authorViewerAsync, 
        editorViewerAsync, 
        reservationViewerAsync, 
        loanViewerAsync,
        userViewerAsync)
