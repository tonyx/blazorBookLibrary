# BiblioNet.eu Architecture and Technical Overview

BiblioNet.eu is a modern software platform dedicated to the cataloging, discovery, and dissemination of library and documentary heritage.

Unlike conventional management systems built on mutable-state relational databases (CRUD), BiblioNet.eu is engineered around the core principles of **functional programming**, **Event Sourcing**, and **open interoperability**. The entire system is built to deliver complete historical auditability, operational scalability, and rigorous transactional consistency.

## 1. Architectural Paradigm: Event Sourcing & CQRS

At the foundation of BiblioNet.eu lies the **Event Sourcing** model paired with the **CQRS** (Command Query Responsibility Segregation) pattern.

Records within the catalog and operational states are never overwritten or destructively deleted. Every lifecycle event across the platform is captured as an immutable, timestamped fact:

* **Command:** The intent to execute an operation (e.g., `CatalogBook`, `RecordLoan`, `AddSubjectHeading`). Commands are formally validated against the aggregate's current state.
* **Event:** The immutable outcome of a validated command, committed sequentially to an append-only store (e.g., `BookCataloged`, `LoanRegistered`).
* **State Evolution:** The current state of any aggregate is computed as a pure function of its past events:

  $$
  \text{State}_{t} = \text{evolve}(\text{State}_{t-1}, \text{Event}_t)
  $$

### Key Advantages for Library Information Systems

* **Built-in Audit Trail:** An unalterable and exhaustive historical record for every physical copy, loan transaction, or bibliographic record.
* **Time Travel & Reproducibility:** The potential ability to reconstruct the exact state of the catalog at any given point in time with mathematical determinism.
* **Resilience to Schema Drifts:** Read-side query projections can be safely refactored and re-indexed simply by replaying the event stream (*event replay*).

## 2. Domain Engine: Sharpino

The domain model and event-handling infrastructure of BiblioNet.eu are powered by **Sharpino**, an open-source library designed for idiomatic Event Sourcing on the .NET ecosystem.

### Key Capabilities of Sharpino

* **F# Native Design:** Leverages an advanced algebraic type system (Discriminated Unions, Immutable Records, exhaustive pattern matching) to eliminate entire classes of runtime errors.
* **PostgreSQL as an Event Store:** Employs battle-tested ACID relational guarantees for append-only log storage, avoiding the operational overhead of specialized distributed streaming clusters.
* **Automatic Snapshotting:** Optimizes aggregate reconstruction performance for entities with extensive event histories, ensuring constant-time state loads.
* **Optimistic Concurrency & Isolation:** Robust transactional semantics to prevent conflicting writes during concurrent operational sessions.

> **Sharpino Open Source Resources:**
>
> * GitHub Repository: [github.com/tonyx/Sharpino](https://github.com/tonyx/Sharpino)
> * NuGet Package: `Sharpino`

## 3. Technology Stack

BiblioNet.eu combines a functional backend with reactive web interfaces and high-performance search capabilities:

| Layer / Component | Technology | Architectural Role |
| :--- | :--- | :--- |
| **Domain Logic** | F# (.NET) | Domain types, command/event contracts, and pure business rule evaluation |
| **Event Persistence** | PostgreSQL | Append-only event storage, transactional streams, and snapshot persistence |
| **Vector Search & AI** | PostgreSQL (`pgvector`) | Semantic text indexing and conceptual similarity matching across titles, abstracts, and metadata |
| **Frontend & UI** | Blazor / C# | Modular, performant web interface integrated within the .NET runtime |

## 4. Open Source Philosophy & Transparency

BiblioNet is founded on the belief that software built for cultural institutions and public knowledge should uphold the same open access principles as the collections it preserves.

* **Auditable Codebases:** Core platform libraries and foundational building blocks are distributed under permissive open-source licenses (MIT / Apache 2.0).
* **Zero Vendor Lock-In:** All stored data remains in open, standardized relational structures. The PostgreSQL-backed event store ensures records can always be extracted, verified, or migrated independently.
* **Deterministic Verification:** Eliminating side effects within the domain layer enables pure algebraic unit testing and automated property-based testing.

## 5. Resources & References

* **Platform Website:** [www.bibionet.eu](https://www.bibionet.eu)
* **Event Sourcing Framework:** [Sharpino on GitHub](https://github.com/tonyx/Sharpino)
* **Biblionet Source code:** [Biblionet - blazorBookLibrary - on GitHub](https://github.com/tonyx/blazorBookLibrary)
* **Technical Contact:** For research partnerships, inter-library interoperability, or security disclosures: `info@bibionet.eu`