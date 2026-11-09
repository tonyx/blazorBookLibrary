module AutoRecoveryTests

open System
open Expecto
open Microsoft.Extensions.Logging.Abstractions
open Sharpino
open Sharpino.Core
open Sharpino.Storage
open Sharpino.MemoryStorage
open Sharpino.Sanitizer
open BookLibrary.Domain
open BookLibrary.Services
open BookLibrary.Shared.Commons

[<Tests>]
let tests =
    testList "AutoRecovery" [
        testCase "AutoRecoveryAnomalyHandler heals a poisoned aggregate stream via NoOp patch on evolve failure" <| fun _ ->
            let storage = MemoryStorage() :> IEventStore<string>
            let bookId = Guid.NewGuid()
            let titleRes = Title.New "Initial Title"
            Expect.isOk titleRes "valid title"
            let title = titleRes |> Result.get

            let initialBook =
                { Book.New (TenantId.Default, title) with
                    BookId = BookId bookId }

            let setInitRes =
                storage.SetInitialAggregateState bookId Book.Version Book.StorageName initialBook.Serialize
            Expect.isOk setInitRes "Initial state setup should succeed"

            // Event 1: Valid title update
            let newTitle1 = (Title.New "Valid Update 1") |> Result.get
            let ev1 = (TitleUpdated (newTitle1, DateTime.UtcNow)).Serialize

            // Event 2: Corrupted raw JSON payload that fails deserialization / evolve
            let ev2_poison = "{\"InvalidPayload\": \"poison_pill_that_fails_evolve\"}"

            // Event 3: Subsequent valid update
            let newTitle2 = (Title.New "Valid Update 2") |> Result.get
            let ev3 = (TitleUpdated (newTitle2, DateTime.UtcNow)).Serialize

            let addRes =
                storage.AddAggregateEventsMd 0 Book.Version Book.StorageName bookId "md" [ ev1; ev2_poison; ev3 ]
            Expect.isOk addRes "Adding raw events to stream should succeed"

            // Create AutoRecoveryAnomalyHandler and register it as Sharpino's anomaly handler
            let recoveryHandler = AutoRecoveryAnomalyHandler(storage, NullLogger<AutoRecoveryAnomalyHandler>.Instance)
            let prevHandler = currentAnomalyHandler
            setAnomalyHandler recoveryHandler

            try
                // First read: encounters the corrupted event during evolve,
                // which fires OnEvolveAnomaly in AutoRecoveryAnomalyHandler,
                // executing sanitizeAggregateStream and patching ev2 to NoOp in-store.
                let firstRead = StateView.getAggregateFreshState<Book, BookEvent, string> bookId storage
                Expect.isError firstRead "Initial read halts when the anomaly was encountered"

                // Second read: since the stream has been healed by replacing the poisoned event with NoOp,
                // evolution succeeds cleanly!
                let secondRead = StateView.getAggregateFreshState<Book, BookEvent, string> bookId storage
                Expect.isOk secondRead "Second read must succeed on the healed stream"

                let (_, healedBook) = secondRead |> Result.get
                Expect.equal healedBook.Title.Value "Valid Update 2" "Book title should evolve to the latest valid event"
            finally
                setAnomalyHandler prevHandler
    ]

