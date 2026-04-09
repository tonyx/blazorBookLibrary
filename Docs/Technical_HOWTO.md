# Technical HOWTO: Infrastructure & Persistence Architecture

This document outlines the technical configuration and maintenance procedures for the Modern Archivist system, specifically focusing on database migrations and distributed state management.

## 1. Database Migrations

The system utilizes two distinct persistence engines, each managed by its own migration tool. Although these typically reside on the same database server, they target separate logical databases.

### 1.1 ASP.NET Identity (EF Core)
User authentication, roles, and identity data are managed via Entity Framework Core.

*   **Migration Files**: Located in `blazorBookLibrary.Data/Migrations`.
*   **Context/Config**: Connection strings are defined in `blazorBookLibrary/appsettings.json`.
*   **Startup Configuration**: Managed in `blazorBookLibrary/Program.cs`.

**Commands**:
To apply migrations or add new ones, you must point the EF tool to both the startup project and the data project:
```bash
# Add a migration
dotnet ef migrations add <MigrationName> --project blazorBookLibrary.Data --startup-project blazorBookLibrary

# Update the database
dotnet ef database update --project blazorBookLibrary.Data --startup-project blazorBookLibrary
```

### 1.2 Snapshots & Event Streams (dbmate)
The core archival domain (Books, Authors, Loans) uses an Event Sourcing pattern managed by Sharpino.

*   **Migration Files**: Located in `blazorBookLibrary.Server/db/migrations`.
*   **Configuration**: Connection credentials are read from `.env` in the `blazorBookLibrary.Server` directory via the `DATABASE_URL` variable.
*   **Target**: This targets a dedicated database for Event Sourcing (distinct from the Identity DB).

**Commands**:
```bash
# Navigate to the server folder
cd blazorBookLibrary.Server

# Apply migrations
dbmate up
```

## 2. Distributed State & Caching

The application architecture includes placeholders for advanced scaling (L2 Cache and Backplanes), though they are currently deactivated in the standard local configuration.

### 2.1 Caching Strategy
The system primarily relies on **L1 (In-Memory)** caching. While parameters for **L2 Cache** (currently envisioned as an **Azure SQL DB**) exist in the configuration files, they are not active. The application does not currently take advantage of secondary cache layers.

### 2.2 Distributed Backplane (Advanced Configuration)
In distributed or multi-node environments, state synchronization is critical.

*   **Propagation**: The L2 backplane can utilize a **Message Bus** (such as Redis or Azure Service Bus) to propagate state change notifications to all connected L1 caches.
*   **Multi-Node Consistency**: Any configuration involving distributed nodes (e.g., employing Azure Functions or remote API calls) **requires** an L2 backplane. Without this backplane, there is a risk of temporary "windows of inconsistency" in the view models across different nodes.

### 2.3 Future Multi-Node Evolution
The system is designed with a future transition to a multi-node, client-heavy architecture in mind:
*   **WebAssembly Client**: Moving towards a Blazor WebAssembly frontend.
*   **REST API Layer**: Interactions will be handled via REST API client wrappers.
*   **Service Wrappers**: The existing service implementations would be substituted with API-based implementations to maintain the same interface while calling remote endpoints.

---
*Note: Ensure that the `.env` file in the server directory is never committed to source control if it contains production credentials.*
