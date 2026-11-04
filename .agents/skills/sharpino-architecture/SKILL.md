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
| **Migration** | `Server/db/migrations/` | `YYYYMMDDHHMMSS_create_<agg>.sql` | Tables, sequences, indexes, and PL/pgSQL functions for event/snapshot append and optimistic locking |

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

### 4.8 SQL Migration for Aggregates (`Server/db/migrations/TIMESTAMP_create_<name>.sql`)

Sharpino event store requires an exact table schema, identity sequences, indexes, and **mandatory PL/pgSQL functions** for event appending and optimistic locking. Direct `INSERT` statements are never used by Sharpino; the runtime calls these stored procedures directly.

> [!IMPORTANT]
> The stream naming follows `{Version}{StorageName}` (e.g. `Version = "_01"` and `StorageName = "_item"` yields `_01_item`). Format is `text` (or `jsonb` if configured). All names unquoted in PostgreSQL resolve to lowercase.

```sql
-- migrate:up

-- 1. Events Table
CREATE TABLE public.events_01_item (
    id integer NOT NULL,
    aggregate_id uuid NOT NULL,
    event text NOT NULL,
    published boolean NOT NULL DEFAULT false,
    distance_from_latest_snapshot integer,
    "timestamp" timestamp without time zone NOT NULL,
    md text
);

ALTER TABLE public.events_01_item ALTER COLUMN id ADD GENERATED ALWAYS AS IDENTITY (
    SEQUENCE NAME public.events_01_item_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1
);

-- 2. Snapshots Table
CREATE SEQUENCE public.snapshots_01_item_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE TABLE public.snapshots_01_item (
    id integer DEFAULT nextval('public.snapshots_01_item_id_seq'::regclass) NOT NULL,
    snapshot text NOT NULL,
    event_id integer,
    aggregate_id uuid NOT NULL,
    "timestamp" timestamp without time zone NOT NULL,
    is_deleted boolean NOT NULL DEFAULT false
);

-- 3. Aggregate Events Junction Table
CREATE SEQUENCE public.aggregate_events_01_item_id_seq
    START WITH 1
    INCREMENT BY 1
    NO MINVALUE
    NO MAXVALUE
    CACHE 1;

CREATE TABLE public.aggregate_events_01_item (
    id integer DEFAULT nextval('public.aggregate_events_01_item_id_seq') NOT NULL,
    aggregate_id uuid NOT NULL,
    event_id integer UNIQUE
);

-- 4. Constraints & Foreign Keys
ALTER TABLE ONLY public.events_01_item
    ADD CONSTRAINT events_01_item_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.snapshots_01_item
    ADD CONSTRAINT snapshots_01_item_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.snapshots_01_item
    ADD CONSTRAINT event_01_item_fk FOREIGN KEY (event_id) REFERENCES public.events_01_item (id) MATCH FULL ON DELETE CASCADE;

ALTER TABLE ONLY public.aggregate_events_01_item
    ADD CONSTRAINT aggregate_events_01_item_pkey PRIMARY KEY (id);

ALTER TABLE ONLY public.aggregate_events_01_item
    ADD CONSTRAINT aggregate_events_01_item_fk FOREIGN KEY (event_id) REFERENCES public.events_01_item (id) MATCH FULL ON DELETE CASCADE;

-- 5. Indexes
CREATE INDEX ix_01_events_item_id ON public.events_01_item(aggregate_id);
CREATE INDEX ix_01_aggregate_events_item_id ON public.aggregate_events_01_item(aggregate_id);
CREATE INDEX ix_01_snapshot_item_id ON public.snapshots_01_item(aggregate_id);
CREATE INDEX ix_01_snapshot_item_aggregate_id_and_id ON public.snapshots_01_item(aggregate_id, id DESC);
CREATE INDEX ix_01_snapshot_item_event_id ON public.snapshots_01_item(event_id);
CREATE INDEX ix_01_events_item_timestamp ON public.events_01_item("timestamp");
CREATE INDEX ix_01_snapshots_item_timestamp ON public.snapshots_01_item("timestamp");

-- 6. Mandatory PL/pgSQL Functions Required by Sharpino Runtime

CREATE OR REPLACE FUNCTION insert_01_item_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_item(event, aggregate_id, timestamp)
    VALUES(event_in, aggregate_id, now()) RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

CREATE OR REPLACE FUNCTION insert_md_01_item_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
BEGIN
    INSERT INTO events_01_item(event, aggregate_id, distance_from_latest_snapshot, timestamp, md)
    VALUES(event_in, aggregate_id, distance_from_latest_snapshot, now(), md) RETURNING id INTO inserted_id;
    RETURN inserted_id;
END;
$$;

CREATE OR REPLACE FUNCTION insert_01_item_aggregate_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid 
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_01_item_event_and_return_id(event_in, aggregate_id);
    INSERT INTO aggregate_events_01_item(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

CREATE OR REPLACE FUNCTION insert_md_01_item_aggregate_event_and_return_id(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text   
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    event_id := insert_md_01_item_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);
    INSERT INTO aggregate_events_01_item(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

CREATE OR REPLACE FUNCTION insert_md_01_item_aggregate_event_and_return_id_opt_lock(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
    found_last_event_id integer;
BEGIN
    SELECT id INTO found_last_event_id 
    FROM events_01_item 
    WHERE events_01_item.aggregate_id = insert_md_01_item_aggregate_event_and_return_id_opt_lock.aggregate_id 
    ORDER BY id DESC LIMIT 1;

    IF last_event_id = 0 THEN
        IF found_last_event_id IS NOT NULL THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected no previous events, but found event %', found_last_event_id;
        END IF;
    ELSIF last_event_id > 0 THEN
        IF found_last_event_id IS NULL OR found_last_event_id <> last_event_id THEN
            RAISE EXCEPTION 'Optimistic locking check failed: expected last event id %, but found %', last_event_id, found_last_event_id;
        END IF;
    END IF;

    event_id := insert_md_01_item_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);
    INSERT INTO aggregate_events_01_item(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

CREATE OR REPLACE FUNCTION insert_md_01_item_aggregate_event_and_return_id_opt_lock2(
    IN event_in text,
    IN aggregate_id uuid,
    IN distance_from_latest_snapshot int,
    IN md text,   
    IN last_event_id int,
    IN extra_stream_names text[],
    IN extra_event_ids int[],
    IN extra_aggregate_ids uuid[]
)
RETURNS int
LANGUAGE plpgsql
AS $$
DECLARE
    inserted_id integer;
    event_id integer;
BEGIN
    -- Perform the main optimistic locking check (with advisory lock serialization)
    PERFORM check_last_event_id_opt_lock('events_01_item', aggregate_id, last_event_id);

    -- Perform the checks for extra cross-aggregate constraints
    IF extra_stream_names IS NOT NULL THEN
        FOR i IN 1..cardinality(extra_stream_names) LOOP
            PERFORM check_last_event_id_opt_lock(extra_stream_names[i], extra_aggregate_ids[i], extra_event_ids[i]);
        END LOOP;
    END IF;

    event_id := insert_md_01_item_event_and_return_id(event_in, aggregate_id, distance_from_latest_snapshot, md);
    INSERT INTO aggregate_events_01_item(aggregate_id, event_id)
    VALUES(aggregate_id, event_id) RETURNING id INTO inserted_id;
    RETURN event_id;
END;
$$;

-- 7. User Grants (if applicable)
GRANT ALL ON TABLE public.aggregate_events_01_item TO safe;
GRANT ALL ON SEQUENCE public.aggregate_events_01_item_id_seq TO safe;
GRANT ALL ON TABLE public.events_01_item TO safe;
GRANT ALL ON SEQUENCE public.events_01_item_id_seq TO safe;
GRANT ALL ON TABLE public.snapshots_01_item TO safe;
GRANT ALL ON SEQUENCE public.snapshots_01_item_id_seq TO safe;

-- migrate:down
```

---

### 4.9 Context Streams vs. Template Automation

#### Context Stream (Singleton / Linear Stream)
When an entity is not keyed per aggregate instance (e.g. system configurations, global singletons), Sharpino uses a **Context Stream**:
- **No `aggregate_events` junction table** and **no `aggregate_id` column**.
- Table: `events{Version}{ContextStorageName}` (`id`, `event`, `published`, `timestamp`, `md`).
- Table: `snapshots{Version}{ContextStorageName}` (`id`, `snapshot`, `event_id`, `timestamp`).
- Functions: `insert{Version}{ContextStorageName}_event_and_return_id` and `insert_md{Version}{ContextStorageName}_event_and_return_id`.

#### Generation from Templates
Rather than handwriting migration SQL, use the canonical template generator from `realsharpino/Sharpino/SqlTemplates/`:
```bash
# cat AggregateTemplate.sql | sed -e "s/{Version}/$1/g" | sed -e "s/{AggregateStorageName}/$2/g" | sed -e "s/{Format}/$3/g"
# Example invocation:
./generateAggregateByTemplate.sh "_01" "_item" "text" > Server/db/migrations/$(date +%Y%m%d%H%M%S)_create_item.sql
```
