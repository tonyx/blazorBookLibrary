# Tech Stack Blueprint — F# Event-Sourced Blazor Solution

> This document serves as a general architectural reference for any solution built on F#, Sharpino (event sourcing), Blazor, and PostgreSQL. Use it as a blueprint when creating or extending features.
>
> All concrete code examples in this document are drawn from the **blazorBookLibrary** reference implementation. When starting from an empty repository, an agentic assistant should consult this repository for working examples of every pattern described here.

## Reference Repository

| | |
|---|---|
| **URL** | https://github.com/tonyx/blazorBookLibrary |
| **Purpose** | Full working implementation of every pattern in this blueprint |
| **When to use** | Browse this repo whenever a pattern description alone is insufficient — check a real aggregate, migration, service, or Razor page for a concrete, compilable example |

> [!IMPORTANT]
> If you are an AI assistant bootstrapping a **new** solution from scratch, treat the reference repository as your primary source of truth for implementation details. The blueprints sections below describe the *patterns*; the repository contains *working code* for each one.

---

## 1. Solution Structure Overview

```
Solution.sln
├── Solution.Shared/          # Domain aggregates, shared interfaces, details composition
│   ├── Domain/               # Aggregate definitions (event-sourced objects)
│   ├── Details/Details.fs    # Composed read-model types (joining multiple aggregates)
│   ├── Services/             # Service interfaces (IXxxService)
│   ├── Commons.fs            # Value objects, IDs, shared domain types
│   └── Resources/            # Localization .resx files
│
├── Solution.Server/          # F# server-side backend
│   ├── Domain/               # Commands and Events per aggregate
│   │   └── Xxx/
│   │       ├── Commands.fs
│   │       └── Events.fs
│   ├── Services/             # Service implementations
│   │   ├── XxxService.fs
│   │   └── DetailsService.fs # Cross-aggregate read-model service
│   ├── Details.fs            # RefreshableXxxDetails wrappers (for DetailsCache)
│   ├── db/
│   │   └── migrations/       # SQL migration files (one per aggregate)
│   └── ...
│
├── Solution/                 # Blazor WebAssembly / Server UI
│   ├── Components/Pages/     # Razor pages
│   ├── appsettings.json      # Feature flags and configuration
│   └── Program.cs            # DI registration
│
└── Solution.Tests/           # Test project
```

---

## 2. Value Objects & Primitive Obsession — `Shared/Commons.fs`

All domain primitives are **wrapped in single-case discriminated unions** defined in `Shared/Commons.fs`. This technique eliminates primitive obsession and enforces compile-time type safety — a `BookId` can never be accidentally passed where a `UserId` is expected.

### Why Value Objects?

Without value objects:
```fsharp
// BAD — all Guids look the same to the compiler:
let addLoan (bookId: Guid) (userId: Guid) (loanId: Guid) = ...
addLoan loanId userId bookId   // compiles silently, wrong at runtime!
```

With value objects:
```fsharp
// GOOD — the compiler catches transposed arguments:
let addLoan (bookId: BookId) (userId: UserId) (loanId: LoanId) = ...
addLoan loanId userId bookId   // ❌ compile error
```

---

### Pattern 1 — Identity Wrappers (Guid-based)

Every aggregate has a dedicated ID type. All follow the same four-line pattern:

```fsharp
type BookId =
    | BookId of Guid
    with
        static member New() = BookId(Guid.NewGuid())
        member this.Value =
            match this with
            | BookId v -> v
```

ID types in this solution:

| Type | Wraps | Used by aggregate |
|---|---|---|
| `BookId` | `Guid` | `Book` |
| `AuthorId` | `Guid` | `Author` |
| `UserId` | `Guid` | `User` |
| `LoanId` | `Guid` | `Loan` |
| `ReservationId` | `Guid` | `Reservation` |
| `ReviewId` | `Guid` | `Review` |
| `EditorId` | `Guid` | `Editor` |
| `IsbnRegistryId` | `Guid` | `IsbnRegistry` |
| `MailQueueId` | `Guid` | `MailQueue` |

> **Rule:** When you add a new aggregate, also add a corresponding `NewAggId` type to `Commons.fs` **before** defining the aggregate record.

---

### Pattern 2 — String Value Objects (with optional validation)

Simple string wrappers enforce semantic intent:

```fsharp
type Title =
    | Title of string
    with
        static member New(title: string) = Title(title)
        member this.Value =
            match this with
            | Title v -> v

type AuthorName =
    | AuthorName of string
    with
        static member New(name: string) = AuthorName(name)
        member this.Value =
            match this with
            | AuthorName v -> v
```

Some string value objects carry **validation logic and multiple cases** to represent valid/invalid/empty states:

```fsharp
type Isbn =
    | Isbn of string           // valid ISBN
    | InvalidIsbn of string    // saved but flagged as invalid
    | EmptyIsbn                // absent
    with
        static member IsValid (isbn: string) = ...  // checksum validation
        static member New (isbn: string) =
            if Isbn.IsValid(isbn) then Ok (Isbn isbn)
            else Error "Invalid ISBN"
        static member NewInvalid (isbn: string) = InvalidIsbn isbn
        static member NewEmpty () = EmptyIsbn
        member this.Value =
            match this with
            | Isbn v | InvalidIsbn v -> v
            | EmptyIsbn -> ""
        member this.IsValidIsbn =
            match this with
            | Isbn _ -> true
            | _ -> false
```

The same multi-case pattern is used for:
- `PhoneNumber` / `InvalidPhoneNumber` / `EmptyPhoneNumber` (regex validation)
- `FiscalCode` / `InvalidFiscalCode` / `EmptyFiscalCode` (Italian CF checksum)
- `Isbn` / `InvalidIsbn` / `EmptyIsbn` (ISBN-10 and ISBN-13 checksums)
- `Isni` / `InvalidIsni` / `EmptyIsni` (ISNI checksum)
- `Name` / `EmptyName` (whitespace guard)

> **Rule:** For any string field that has a well-defined validity rule, prefer a multi-case DU over `Option<string>` or `string`. The `Invalid*` case lets data be stored even when invalid (e.g., user-entered data awaiting correction), while still being distinguishable from a `Valid*` case at the type level.

---

### Pattern 3 — Domain Enumerations

Closed sets of domain values are also value objects:

```fsharp
type Availability =
    | Circulating
    | ReferenceOnly
    | Unspecified
    with
        static member AllCases () = [ Circulating; ReferenceOnly; Unspecified ]
        static member FromString (s: string) = ...

type ApprovalStatus =
    | Pending
    | Approved of DateTime   // carries the approval timestamp
    | Rejected of DateTime

type LoanStatus =
    | InProgress
    | Returned of DateTime

type ReservationStatus =
    | Pending
    | Loaned
```

Enumerations that carry data (like `Approved of DateTime`) are richer than C# enums — the timestamp is part of the type, not a separate field.

---

### Pattern 4 — Composite/Behaviour Value Objects

Some value objects encapsulate domain behaviour:

```fsharp
type TimeSlot =
    {
        Start: DateTime
        End: DateTime
    }
    with
        static member New (start: DateTime) (endTime: DateTime) = ...
        member this.IsFutureOf (dateNow: DateTime) = this.Start > dateNow
        member this.Overlaps (other: TimeSlot) =
            this.Start < other.End && other.Start < this.End
        member this.Shift (dateTime: DateTime) =
            { this with Start = dateTime; End = dateTime + (this.End - this.Start) }

type Sealed =           // tracks seal/unseal lifecycle of an aggregate
    {
        DateTime: DateTime
        Sealed: bool
    }
    with
        member this.IsSealed (dateNow: DateTime) = this.Sealed
        member this.Seal (dateTime: DateTime) = { this with Sealed = true; DateTime = dateTime }
        member this.Unseal (dateTime: DateTime) = { this with Sealed = false; DateTime = dateTime }
```

---

### Extracting the Raw Value

Whenever the underlying primitive is needed (e.g., passing to the event store or PostgreSQL), always use `.Value`:

```fsharp
// Reading an aggregate from the store:
let! book = bookViewerAsync (Some ct) bookId.Value   // bookId.Value : Guid

// Constructing an ID from a raw Guid (e.g., from URL param):
let bookId = BookId.NewBookId(guid)     // Sharpino-generated factory
// or:
let bookId = BookId guid                // direct wrapping

// Constructing from a user-supplied string Guid:
if Guid.TryParse(userIdStr, out var userGuid) then
    let userId = UserId.NewUserId(userGuid)
```

> **Rule:** Never pass raw `Guid` or `string` values across service or aggregate boundaries. Always wrap into the appropriate value object as early as possible (e.g., at the Razor page boundary when parsing URL parameters).

---

## 3. Aggregate (Event-Sourced Object) — `Shared/Domain/Xxx.fs`

Each **aggregate** (also called: event-sourced object, domain object, stream root) lives in the **Shared project** so it can be referenced by both the Server and Client.

### Naming & Identity Conventions

Every aggregate **must** declare:
- `static member StorageName = "_Xxx"` — base name used to compose PostgreSQL table and function names
- `static member Version = "_01"` — version prefix used in table and function names
- `static member SnapshotsInterval = N` — how many events between snapshots
- `member this.Id = this.XxxId.Value` — Guid-based identity
- `member this.Serialize` / `static member Deserialize` — JSON round-trip using shared `jsonOptions`

### Aggregate Example

```fsharp
namespace MyDomain

type Review =
    {
        ReviewId: ReviewId
        BookId: BookId
        UserId: UserId
        Comment: string
        Hidden: bool
        ApprovalStatus: ApprovalStatus
    }
    with
        static member SnapshotsInterval = 50
        static member StorageName = "_Review"   // → tables: events_01_Review, snapshots_01_Review
        static member Version = "_01"
        member this.Id = this.ReviewId.Value
        member this.Serialize = JsonSerializer.Serialize(this, jsonOptions)
        static member Deserialize json = ...
        // domain methods returning Result<'T, string>
        member this.Approve dateTime = { this with ApprovalStatus = Approved dateTime } |> Ok
```

> **Rule:** All mutable state transitions return `Result<Aggregate, string>`, never throw.

---

## 4. Migration Files — `Server/db/migrations/`

Each aggregate has **exactly one migration file**. The file name format is:
```
YYYYMMDDHHMMSS_create_AggregateName.sql
```

### What the Migration Creates

The table and function names are composed from `Version + StorageName` of the aggregate:

| Aggregate static member | Example value |
|---|---|
| `Version` | `"_01"` |
| `StorageName` | `"_Review"` |
| **Resulting table prefix** | `_01_Review` |

**Tables created:**
- `events_01_Review` — append-only event log
- `snapshots_01_Review` — periodic aggregate state snapshots
- `aggregate_events_01_Review` — links aggregate IDs to event IDs

**Functions created:**
- `insert_01_Review_event_and_return_id(event_in, aggregate_id)`
- `insert_md_01_Review_event_and_return_id(event_in, aggregate_id, distance, md)`
- `insert_01_Review_aggregate_event_and_return_id(event_in, aggregate_id)`
- `insert_md_01_Review_aggregate_event_and_return_id(event_in, aggregate_id, distance, md)`

> **Rule:** When adding a new aggregate, create both the F# type (with `StorageName` + `Version`) and the corresponding migration that uses the same naming. They must be in sync.

---

## 5. Domain Commands & Events — `Server/Domain/Xxx/`

Each aggregate gets its own folder in `Server/Domain/` containing exactly two files:

### `Commands.fs`

Defines a discriminated union implementing `AggregateCommand<Aggregate, Event>`:

```fsharp
type ReviewCommand =
    | Approve of DateTime
    | Reject of DateTime
    | Edit of string
    | Hide
    | Show

    interface AggregateCommand<Review, ReviewEvent> with
        member this.Execute (review: Review) =
            match this with
            | Approve dateTime ->
                review.Approve dateTime
                |> Result.map (fun r -> (r, [ReviewApproved dateTime]))
            | ...
        member this.Undoer = None
```

### `Events.fs`

Defines a discriminated union implementing `AggregateEvent<Aggregate>`:

```fsharp
type ReviewEvent =
    | ReviewApproved of DateTime
    | ReviewRejected of DateTime
    | ReviewEdited of string
    | ReviewHidden
    | ReviewShown

    interface AggregateEvent<Review> with
        member this.Process (review: Review) =
            match this with
            | ReviewApproved dateTime -> { review with ApprovalStatus = Approved dateTime } |> Ok
            | ...
```

> **Rule:** Each command produces a `(newState, events list)` tuple. Each event replays deterministically on the aggregate.

---

## 6. Service Interfaces — `Shared/Services/IXxxService.fs`

Service interfaces live in the **Shared project** and are consumed by both the Server implementation and the Blazor UI (via DI injection).

```fsharp
namespace MyApp.Shared.Services

type IReviewService =
    abstract member AddReviewAsync: review:Review * ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member ApproveReviewAsync: id:ReviewId * ?ct:CancellationToken -> Task<Result<unit, string>>
    abstract member GetApprovedVisibleReviewsOfBookAsync: bookId:BookId * ?ct:CancellationToken -> Task<Result<List<Review * _>, string>>
```

- Use `[<Optional; DefaultParameterValue(null)>] ?ct:CancellationToken` for optional cancellation tokens
- All return types are `Task<Result<'T, string>>` — **never throw, always wrap errors**

---

## 7. Service Implementations — `Server/Services/XxxService.fs`

Service implementations live in the **Server project** and receive:
- `IEventStore<string>` — the Sharpino PostgreSQL event store
- `AggregateViewerAsync2<TAgg>` — typed stream state reader (see below)
- Optional: other service interfaces as collaborators

### Aggregate State Reading Pattern

```fsharp
// Built during service construction — one viewer per aggregate type
let bookViewerAsync =
    getAggregateStorageFreshStateViewerAsync<Book, BookEvent, string> eventStore

// Usage inside service methods:
let! book =
    bookViewerAsync (Some ct) bookId.Value
    |> TaskResult.map snd       // snd = the aggregate state (fst = version)
```

> **Rule:** Always use `AggregateViewerAsync2<TAgg>` (the async variant) and always call with `(Some ct)` for cancellation support.

### Multi-Aggregate Command Pattern

When a business operation must update multiple aggregates atomically, use Sharpino's multi-command runners:

```fsharp
// Two aggregates updated in one transaction:
let! result =
    runInitAndTwoAggregateCommandsMd<Book, BookEvent, User, UserEvent, string, Reservation>
        book.Id
        user.Id
        eventStore
        messageSenders
        reservation      // initial aggregate to persist (e.g. a new Reservation)
        ""               // metadata
        addReservationToBookCommand
        addReservationToUserCommand

// Delete one + update two:
let! result =
    runDeleteAndTwoAggregateCommandsMd<Reservation, ReservationEvent, Book, BookEvent, User, UserEvent, string>
        eventStore messageSenders ""
        reservationId.Value book.Id user.Id
        removeReservationFromBook removeReservationFromUser
        (fun _ -> true)
```

### Service Independence

- Domain services (`BookService`, `LoanService`, `ReservationService`, `ReviewService`, `UserService`, etc.) are **as independent as possible** from each other.
- They may accept other services as constructor arguments **only when orchestration is required** (e.g., `ReservationService` accepts `IUserService` to verify user limits and send emails).
- They **do not** call `DetailsService`.

---

## 8. Details Composition — `Shared/Details/Details.fs`

The `Details` module in the **Shared project** defines **read-model types** that join data from multiple aggregates. These are pure data records, not event-sourced objects.

```fsharp
module Details =
    // Joins User (aggregate) + ApplicationUser (ASP.NET Identity) + related aggregates
    type UserDetails =
        {
            User: User
            ApplicationUser: ApplicationUser
            FutureReservations: List<Reservation * Book>
            CurrentLoans: List<Loan * Book>
            BooksAndReviews: List<Book * Review>
        }

    // Joins Book + Authors + CurrentLoan + ReservationsDetails + ApprovedReviews
    type BookDetails =
        {
            Authors: List<Author>
            Book: Book
            CurrentLoan: Option<LoanDetails>
            ReservationsDetails: List<ReservationDetails>
            ApprovedVisibleReviews: List<ReviewDetails>
        }
```

> **Rule:** Details types are read-only projections. They carry convenience members (e.g., `member this.HasAnApprovedReviewOfBook`) but contain no commands or state mutations.

---

## 9. Refreshable Details — `Server/Details.fs`

The Server project wraps every `XxxDetails` type into a `RefreshableXxxDetails` record. These types:
1. Hold a `Refresher: unit -> Result<XxxDetails, string>` function closure
2. Implement the `Refreshable<RefreshableXxxDetails>` interface (required by `DetailsCache`)
3. Enable cache invalidation and lazy refresh without rebuilding from scratch

### Pattern

```fsharp
namespace MyApp.Details

module Details =
    type RefreshableBookDetails =
        {
            BookDetails: BookDetails
            Refresher: unit -> Result<BookDetails, string>
        }
        member this.Refresh () =
            result {
                let! bookDetails = this.Refresher ()
                return { this with BookDetails = bookDetails }
            }
        interface Refreshable<RefreshableBookDetails> with
            member this.Refresh () = this.Refresh ()
```

One `RefreshableXxxDetails` type exists per details type:
`RefreshableUserDetails`, `RefreshableBookDetails`, `RefreshableAuthorDetails`, `RefreshableLoanDetails`, `RefreshableReservationDetails`, `RefreshableReviewDetails`, …

---

## 10. DetailsService — `Server/Services/DetailsService.fs`

`DetailsService` is the **central read-model service**. It:
- Depends on `ILoanService`, `IReservationService`, `IReviewService` (and others)
- Holds `AggregateViewerAsync2<TAgg>` viewers for all aggregate types
- Implements `IDetailsService` (defined in Shared)

### The Two-Phase Pattern (Refresher + Cache)

For each details type, the service defines a private `GetRefreshableXxxDetailsAsync` method:

```fsharp
member private this.GetRefreshableBookDetailsAsync(bookId: BookId, ?ct) =
    let detailsBuilder =
        fun (ct: Option<CancellationToken>) ->
            let refresher =
                fun () ->
                    // Synchronous taskResult block that reads all viewers
                    taskResult {
                        let! book = bookViewerAsync (Some ct_) bookId.Value |> TaskResult.map snd
                        let! authors = ...
                        return { Book = book; Authors = authors; ... }
                    }
                    |> Async.AwaitTask |> Async.RunSynchronously
            result {
                let! bookDetails = refresher ()
                return
                    { BookDetails = bookDetails; Refresher = refresher }
                    :> Refreshable<RefreshableBookDetails>
                    ,
                    // Cache dependency keys (invalidate cache if any of these aggregate IDs change):
                    bookId.Value :: (bookDetails.Authors |> List.map _.AuthorId.Value)
            }
    let key = DetailsCacheKey.OfType typeof<RefreshableBookDetails> bookId.Value
    task { return StateView.getRefreshableDetailsAsync<RefreshableBookDetails> (fun ct -> detailsBuilder ct) key ct }
```

The public `GetBookDetailsAsync` unwraps the refreshable wrapper:
```fsharp
member this.GetBookDetailsAsync(bookId, ?ct) =
    taskResult {
        let! refreshable = this.GetRefreshableBookDetailsAsync(bookId, ct)
        return refreshable.BookDetails
    }
```

> **Rule:** Always provide cache dependency keys — the list of all aggregate IDs whose changes should invalidate this details cache entry.

---

## 11. Configuration Pattern — `appsettings.json`

Feature flags and domain settings live under a named section:

```json
{
  "BooksLibrary": {
    "ReviewSytemEnabled": true,
    "TimeSlotLoanDurationInDays": 15,
    "MaxReservationsPerUser": 20,
    "MaxLoansPerUser": 10,
    "EmailNotificationEnabled": true,
    "FromEmail": "noreply@example.com",
    "FromName": "My Library"
  }
}
```

Read in F# services via constructor injection:
```fsharp
let maxReservations = configuration.GetValue<int>("BooksLibrary:MaxReservationsPerUser", 3)
```

Read in Blazor Razor pages via `@inject IConfiguration Configuration`:
```csharp
var isEnabled = Configuration.GetValue<bool>("BooksLibrary:ReviewSytemEnabled");
```

---

## 12. Blazor UI Conventions

### Feature Flag Gate Pattern (Razor pages)
```razor
@inject IConfiguration Configuration

@code {
    private bool isFeatureEnabled;

    protected override void OnInitialized() {
        isFeatureEnabled = Configuration.GetValue<bool>("Section:FeatureEnabled");
    }
}
```

### Access Control Pattern (protected pages)
For pages that should only be accessible when a feature flag is `true`, check early in `OnInitializedAsync`:
```csharp
var featureEnabled = Configuration.GetValue<bool>("BooksLibrary:ReviewSytemEnabled");
if (!featureEnabled) {
    isAllowed = false;
    errorMessage = L["ReviewSystemDisabled"];
    isLoading = false;
    return;
}
```

### Service Injection
- Inject service **interfaces** (from `Shared/Services/`), never concrete types
- Use `@inject IDetailsService DetailsService` for composed read-models
- Use `@inject ILogger<PageName> Logger` (not `Logger<T>`)

### Localization
- All user-visible strings come from `IStringLocalizer<SharedResources> L`
- Add keys to **all** `.resx` files: `SharedResources.resx` (default), `.it-IT.resx`, `.en-US.resx`

---

## 13. Aggregate-to-Migration Traceability

| Aggregate | `Version` | `StorageName` | Migration file |
|---|---|---|---|
| `Book` | `_01` | `_Book` | `..._create_Book.sql` |
| `Author` | `_01` | `_Author` | `..._create_Author.sql` |
| `Loan` | `_01` | `_Loan` | `..._create_loan.sql` |
| `Reservation` | `_01` | `_Reservation` | `..._create_Reservation.sql` |
| `Review` | `_01` | `_Review` | `..._create_review.sql` |
| `User` | `_01` | `_User` | `..._create_user.sql` |
| `Editor` | `_01` | `_Editor` | `..._create_Editor.sql` |
| `IsbnRegistry` | `_01` | `_IsbnRegistry` | `..._create_isbn_registry.sql` |

> **Rule:** The PostgreSQL table `events_{Version}{StorageName}` must always match the aggregate's static members. If you bump `Version`, create a new migration.

---

## 14. Checklist — Adding a New Aggregate

- [ ] Add `NewAggId` (single-case DU wrapping `Guid`) to `Shared/Commons.fs`
- [ ] Add any new domain value objects to `Shared/Commons.fs` (string wrappers, enumerations, composite value objects)
- [ ] Create `Shared/Domain/NewAgg.fs` — define the F# record using the new value object IDs, with `StorageName`, `Version`, `SnapshotsInterval`, `Id`, `Serialize`, `Deserialize`
- [ ] Create `Server/Domain/NewAgg/Commands.fs` — `NewAggCommand` DU implementing `AggregateCommand<NewAgg, NewAggEvent>`
- [ ] Create `Server/Domain/NewAgg/Events.fs` — `NewAggEvent` DU implementing `AggregateEvent<NewAgg>`
- [ ] Create `Server/db/migrations/TIMESTAMP_create_NewAgg.sql` — tables and functions using `{Version}{StorageName}` naming
- [ ] Create `Shared/Details/Details.fs` additions — `NewAggDetails` type if cross-aggregate composition is needed
- [ ] Create `Server/Details.fs` addition — `RefreshableNewAggDetails` wrapping the details type
- [ ] Create `Shared/Services/INewAggService.fs` — interface with `Task<Result<...>>` members
- [ ] Create `Server/Services/NewAggService.fs` — implementation using `AggregateViewerAsync2` and `runXxxCommandsMd` helpers
- [ ] Update `DetailsService.fs` if the new aggregate participates in any details composition
- [ ] Register the service in `Program.cs` (DI container)
- [ ] Add localization keys to all `.resx` files for any new UI strings

---

## 15. GDPR Anonymization Pattern (Ghosting)

When users must be removed from the system while preserving the integrity of historical event streams (e.g., loans, reviews, reservations), use the **Anonymization (Ghosting) Pattern** instead of physical deletion.

1. **Identity Layer**: Clear PII from the ASP.NET Identity record (`ApplicationUser`). Set `UserName` and `Email` to randomized strings (e.g., `ghosted_abc123`), clear fields like `Nome`, `Cognome`, and `CodiceFiscale`, and permanently lock the account.
2. **Domain Layer**: The `User` aggregate remains in the event store. Other aggregates that reference the `UserId` (like `Loan` or `Review`) remain valid and resolvable, ensuring historical consistency.
3. **Execution**: Use `IUserService.GhostUserAsync` to coordinate the identity anonymization and the domain ghosting event.
