---
name: sharpino-architecture
description: Core architectural invariants, negative constraints, type/naming conventions, and canonical skeleton templates for F# Sharpino event-sourced slices, aggregates, commands, events, and services in blazorBookLibrary.
---

# Sharpino Event-Sourced Architecture (F# / Blazor / PostgreSQL)

Compact architectural specification and canonical code skeletons for building feature slices, event-sourced aggregates, commands, events, services, and read models compliant with `blazorBookLibrary`.

---

## 1. Architectural Invariants

1. **Total Domain Purity & Determinism**: Aggregates and domain entities are pure, immutable F# records without I/O, database dependencies, or non-deterministic calls. Timestamps (`DateTime`) and identifiers (`Guid`) are injected as arguments via commands.
2. **Result-Driven Control Flow**: Domain mutations return `Result<'T, string>`; service methods return `Task<Result<'T, string>>` (or `TaskResult<'T, string>`). Never throw exceptions for expected domain errors; use `FsToolkit.ErrorHandling` (`result { ... }`, `taskResult { ... }`).
3. **Multi-Tenant Boundary Isolation**: Aggregates bound to a circuit carry `TenantId: TenantId`. Service operations require `context: UserContext`, verify tenant authorization via `userTenantResolverService` and `Security.check*`, and ensure entity-tenant alignment before mutation.
4. **Command-Event Sourced Transitions**:
   - Commands implement `AggregateCommand<TAgg, TEvent>`: validate business invariants and emit events `Result<TAgg * TEvent list, string>`.
   - Events implement `Event<TAgg>`: rehydrate state via `Process: TAgg -> Result<TAgg, string>` deterministically.
   - Schema evolution uses versioned DUs or upcast records (`TAgg001.Upcast()`) in `Deserialize`.
5. **Aggregate Boundary & Isolation**: Aggregates never hold direct references to other aggregate roots; foreign relationships exist strictly via identity value objects (`TAggId`). Multi-aggregate transactional atomicity is coordinated via Sharpino command runners (`runTwoAggregateCommandsMdAsync`).
6. **Dual-Model CQRS**: Write operations evolve state strictly via event streams (`runAggregateCommandMdAsync`, `runInitAsync`). Read operations query streams via `AggregateViewerAsync2<TAgg>` or composite read models (`Details.fs`) wrapped in `RefreshableAsync<'T>` with `DetailsCache` invalidation.
7. **Auxiliary Store Decoupling & Ghosting**: Secondary projections (e.g. `pgvector` for semantic embeddings, ASP.NET Identity) are downstream views reconciled with the event store. Deletion of personal identities follows the GDPR "Ghosting" pattern (anonymizing Identity, retaining aggregate ID references in event history).

---

## 2. Anti-patterns / Negative Constraints

- **NEVER throw exceptions** in domain logic, command handlers, event processors, or services. Catch external I/O/deserialization exceptions and map to `Error string`.
- **NEVER perform I/O or non-deterministic calls** (`DateTime.UtcNow`, `Guid.NewGuid()`, DB/HTTP) inside aggregate members or event processors. Pass timestamps and IDs via commands.
- **NEVER use mutable state**: no `mutable`, `ref`, or mutable collections (`ResizeArray`) in domain models. Use immutable records, `List`, `Map`, `Set`, and `{ aggregate with Field = value }`.
- **NEVER commit Primitive Obsession**: Never pass raw `Guid`, `string`, or `int` across aggregate or service boundaries. Always wrap in single-case Discriminated Unions (`BookId`, `TenantId`, `Title`, `Isbn`).
- **NEVER bypass Multi-Tenant Isolation**: Never execute commands or queries without validating `TenantId` against `UserContext` and tenant permissions.
- **NEVER bypass the Event Store**: No direct SQL `INSERT`/`UPDATE` on aggregate tables. All state evolution occurs via Sharpino command execution.
- **NEVER nest aggregate roots**: Do not embed aggregate records within other aggregates. Use ID references.
- **NEVER physically delete records with event history**: Apply the Ghosting pattern for user departure to preserve event stream integrity.

---

## 3. Type & Domain Conventions

| Category | File Location | Naming Pattern | Structure / Form |
|---|---|---|---|
| **Identity VO** | `Shared/Slices/Common/Commons.fs` | `<Aggregate>Id` | `type XxxId = XxxId of Guid with static member New() ... member this.Value` |
| **Domain VO** | `Shared/Slices/Common/Commons.fs` | `<Concept>` | Single/Multi-case DU or record with validation returning `Result<'T, string>` |
| **Aggregate** | `Shared/Slices/<Feature>/<Aggregate>.fs` | `<Aggregate>` | Pure record; Sharpino members: `Id`, `StorageName = "_xxx"`, `Version = "_01"`, `SnapshotsInterval = N`, `Serialize`, `Deserialize` |
| **Command** | `Server/Slices/<Feature>/Commands.fs` | `<Aggregate>Command` | DU with imperative verbs (`AddXxx`, `UpdateXxx`); implements `AggregateCommand<TAgg, TEvent>` |
| **Event** | `Server/Slices/<Feature>/Events.fs` | `<Aggregate>Event` | DU with past-tense verbs (`XxxAdded`, `XxxUpdated`); implements `Event<TAgg>` + JSON ser/deser |
| **Read Model** | `Shared/Slices/Details/Details.fs` | `<Aggregate>Details` | Pure record joining multiple aggregates / VOs for UI consumption |
| **Cached Model**| `Server/Slices/Details/Details.fs` | `Refreshable<Agg>Details` | Record with `Refresher: Option<CancellationToken> -> TaskResult<...>` implementing `RefreshableAsync` |
| **Service Contract** | `Shared/Slices/<Feature>/I<Agg>Service.fs`| `I<Agg>Service` | Interface taking `context: UserContext`, `?ct: CancellationToken`, returning `TaskResult<'T, string>` |
| **Service Impl** | `Server/Slices/<Feature>/<Agg>Service.fs` | `<Agg>Service` | Class consuming `IEventStore<string>`, `MessageSenders`, `AggregateViewerAsync2<TAgg>`, `IUserTenantResolverService` |
| **Migration** | `Server/db/migrations/` | `YYYYMMDDHHMMSS_create_<agg>.sql` | Tables: `events_{Version}{StorageName}`, `snapshots_{Version}{StorageName}`, `aggregate_events_...` |

---

## 4. Canonical Skeleton Templates

### 4.1 Value Object (`Shared/Slices/Common/Commons.fs`)
```fsharp
type ItemId =
    | ItemId of Guid
    with
        static member New() = ItemId(Guid.NewGuid())
        member this.Value = match this with ItemId v -> v

type ItemName =
    | ItemName of string
    with
        static member New (str: string) =
            if String.IsNullOrWhiteSpace str then Error "Item name cannot be empty"
            else Ok (ItemName (str.Trim()))
        member this.Value = match this with ItemName v -> v
```

### 4.2 Aggregate Root (`Shared/Slices/<Feature>/<Aggregate>.fs`)
```fsharp
namespace BookLibrary.Domain

open System
open System.Text.Json
open FsToolkit.ErrorHandling
open Sharpino
open BookLibrary.Shared.Commons

type Item =
    {
        TenantId: TenantId
        ItemId: ItemId
        Name: ItemName
        IsActive: bool
    }
    with
        static member New (tenantId: TenantId) (name: ItemName) : Item =
            { TenantId = tenantId; ItemId = ItemId.New(); Name = name; IsActive = true }

        member this.Rename (newName: ItemName) (dateTime: DateTime) : Result<Item, string> =
            Ok { this with Name = newName }

        member this.Deactivate (dateTime: DateTime) : Result<Item, string> =
            if not this.IsActive then Error "Item is already inactive"
            else Ok { this with IsActive = false }

        // Sharpino contract members
        member this.Id = this.ItemId.Value
        static member Version = "_01"
        static member StorageName = "_item"
        static member SnapshotsInterval = 50
        member this.Serialize = JsonSerializer.Serialize(this, jsonOptions)
        static member Deserialize (data: string) : Result<Item, string> =
            try JsonSerializer.Deserialize<Item>(data, jsonOptions) |> Ok
            with ex -> Error $"Deserialization error: {ex.Message}"
```

### 4.3 Command DU (`Server/Slices/<Feature>/Commands.fs`)
```fsharp
namespace BookLibrary.Domain

open System
open Sharpino.Core
open BookLibrary.Shared.Commons

type ItemCommand =
    | Rename of newName: ItemName * DateTime
    | Deactivate of DateTime

    interface AggregateCommand<Item, ItemEvent> with
        member this.Execute (item: Item) =
            match this with
            | Rename (newName, dt) ->
                item.Rename newName dt
                |> Result.map (fun updated -> updated, [ItemRenamed (newName, dt)])
            | Deactivate dt ->
                item.Deactivate dt
                |> Result.map (fun updated -> updated, [ItemDeactivated dt])
        member this.Undoer = None
```

### 4.4 Event DU (`Server/Slices/<Feature>/Events.fs`)
```fsharp
namespace BookLibrary.Domain

open System
open System.Text.Json
open Sharpino.Core
open BookLibrary.Shared.Commons

type ItemEvent =
    | ItemRenamed of newName: ItemName * DateTime
    | ItemDeactivated of DateTime

    interface Event<Item> with
        member this.Process (item: Item) : Result<Item, string> =
            match this with
            | ItemRenamed (newName, dt) -> item.Rename newName dt
            | ItemDeactivated dt -> item.Deactivate dt

    member this.Serialize = JsonSerializer.Serialize(this, jsonOptions)
    static member Deserialize (data: string) : Result<ItemEvent, string> =
        try JsonSerializer.Deserialize<ItemEvent>(data, jsonOptions) |> Ok
        with ex -> Error ex.Message
```

### 4.5 Service Interface (`Shared/Slices/<Feature>/I<Aggregate>Service.fs`)
```fsharp
namespace BookLibrary.Shared.Services

open System
open System.Threading
open System.Threading.Tasks
open System.Runtime.InteropServices
open FsToolkit.ErrorHandling
open BookLibrary.Domain
open BookLibrary.Shared.Commons

type IItemService =
    abstract member AddItemAsync:
        context: UserContext * name: ItemName * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
        -> TaskResult<ItemId, string>

    abstract member RenameItemAsync:
        context: UserContext * itemId: ItemId * newName: ItemName * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
        -> TaskResult<unit, string>

    abstract member GetItemAsync:
        context: UserContext * itemId: ItemId * [<Optional; DefaultParameterValue(null)>] ?ct: CancellationToken
        -> Task<Result<Item, string>>
```

### 4.6 Service Implementation (`Server/Slices/<Feature>/<Aggregate>Service.fs`)
```fsharp
namespace BookLibrary.Services

open System
open System.Threading
open System.Threading.Tasks
open FsToolkit.ErrorHandling
open Sharpino
open Sharpino.Core
open Sharpino.CommandHandler
open Sharpino.Storage
open Sharpino.EventBroker
open BookLibrary.Domain
open BookLibrary.Shared.Commons
open BookLibrary.Shared.Services

type ItemService
    (
        eventStore: IEventStore<string>,
        messageSenders: MessageSenders,
        itemViewerAsync: AggregateViewerAsync2<Item>,
        tenantViewerAsync: AggregateViewerAsync2<Tenant>,
        userTenantResolverService: IUserTenantResolverService
    ) =

    let checkAuthorization (context: UserContext) (ct: CancellationToken) =
        taskResult {
            let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
            let! tenant = tenantViewerAsync (Some ct) tenantId.Value |> TaskResult.map snd
            return! Security.checkIsGlobalAdminOrTenantManager tenant context
        }

    interface IItemService with
        member this.GetItemAsync(context: UserContext, itemId: ItemId, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                let! (_, item) = itemViewerAsync (Some ct) itemId.Value
                do! item.TenantId = tenantId |> Result.ofBool "Tenant mismatch"
                return item
            }

        member this.AddItemAsync(context: UserContext, name: ItemName, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! tenantId = userTenantResolverService.GetTenantForUserAsync(context, ct)
                do! checkAuthorization context ct
                let item = Item.New tenantId name
                do! runInitAsync<Item, ItemEvent, string> eventStore messageSenders item (Some ct)
                return item.ItemId
            }

        member this.RenameItemAsync(context: UserContext, itemId: ItemId, newName: ItemName, ?ct: CancellationToken) =
            let ct = defaultArg ct CancellationToken.None
            taskResult {
                let! item = (this :> IItemService).GetItemAsync(context, itemId, ct)
                do! checkAuthorization context ct
                let cmd = ItemCommand.Rename (newName, DateTime.UtcNow)
                return!
                    runAggregateCommandMdAsync<Item, ItemEvent, string>
                        itemId.Value
                        eventStore
                        messageSenders
                        context.Metadata
                        cmd
                        (Some ct)
            }
```

### 4.7 Details Read Model & Cache Wrapper (`Shared` & `Server`)
```fsharp
// Shared/Slices/Details/Details.fs
type ItemDetails =
    { Item: Item; RelatedSummary: string }

// Server/Slices/Details/Details.fs
type RefreshableItemDetails =
    {
        ItemDetails: ItemDetails
        Refresher: Option<CancellationToken> -> TaskResult<ItemDetails, string>
    }
    member this.RefreshAsync ct =
        taskResult {
            let! details = this.Refresher ct
            return { this with ItemDetails = details }
        }
    interface RefreshableAsync<RefreshableItemDetails> with
        member this.RefreshAsync ct = this.RefreshAsync ct
```

### 4.8 SQL Migration (`Server/db/migrations/TIMESTAMP_create_<name>.sql`)
```sql
-- Pattern: {Version}{StorageName} -> 01_item
CREATE TABLE IF NOT EXISTS events_01_item (
    id UUID PRIMARY KEY,
    aggregate_id UUID NOT NULL,
    event TEXT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE TABLE IF NOT EXISTS snapshots_01_item (
    aggregate_id UUID PRIMARY KEY,
    snapshot TEXT NOT NULL,
    version INT NOT NULL,
    timestamp TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE TABLE IF NOT EXISTS aggregate_events_01_item (
    aggregate_id UUID NOT NULL,
    event_id UUID NOT NULL REFERENCES events_01_item(id) ON DELETE CASCADE,
    PRIMARY KEY (aggregate_id, event_id)
);
```
