# Project Features & Architectural Highlights: Blazor Book Library

This document provides a comprehensive overview of the features and internal structure of the **Blazor Book Library**. It is designed to serve as a reference for presentations and technical summaries.

---

## 1. Project Overview
The Blazor Book Library is a state-of-the-art archival and lending system built with a "Privacy by Design" and "AI-First" philosophy. It combines the reliability of **Event Sourcing** with the cutting-edge capabilities of **Generative AI** and **Semantic Search**.

---

## 2. User Roles & Capabilities

### 👤 The End User (Patron)
*Focused on discovery and seamless borrowing.*

*   **Advanced Catalog Search**: traditional filtering by Title, ISBN, Year, and Category.
*   **AI Semantic Discovery & Multi-Paper Search**: Search by meaning rather than keywords. Users can describe what they are looking for (e.g., *"a tragic love story set during the industrial revolution"* or *"quantum entanglement in photosynthetic complexes"*) and the system finds conceptually relevant books and individual embedded articles/papers.
*   **Conversational AI Librarian (RAG-Powered Chatbot)**: An interactive AI literary scholar combining Retrieval-Augmented Generation (RAG) and Gemini models. Engages patrons in multi-turn dialogues, answers research questions grounded in the library's actual catalog, provides direct book links and paper citations, and features an optional general knowledge mode with voice/speech capabilities.
*   **Geographical Proximity & Library Discovery**: Patrons can set their optional location (via browser GPS, interactive map, or manual coordinates) to filter and discover public libraries and community book-sharing circuits ordered by physical proximity.
*   **Tag-Based Filtering**: Flexible discovery using archival tags (e.g., "First Edition", "Local Interest") with support for logical disjunction (OR) filtering.
*   **Real-time Availability**: Instant feedback on whether a book is "Immediately Available", "Reference Only", or "Currently Loaned".
*   **Self-Service Loans & Reservations**: Members can borrow available titles or reserve books currently on loan, receiving automated notifications when they become available.
*   **Personal Dashboard**: A unified view of current loans, pending reservations, and personal reading history.

### 📚 The Manager (Librarian)
*Focused on catalog excellence and data integrity.*

*   **Multi-Paper Books & Article Indexing ("Papers")**:
    *   **Catalog Individual Contributions**: Register distinct papers, essays, and articles within collective works (anthologies, conference proceedings, scholarly journals) with their own authors and titles.
    *   **Camera OCR Table-of-Contents Scan**: Photograph a book's printed table of contents or index; AI vision extracts article titles and authors for one-click addition.
    *   **Abstract & Content Embeddings**: Generate high-dimensional vector embeddings of paper abstracts or transcribed texts into PostgreSQL pgvector, making individual articles directly reachable by semantic search and the AI Librarian.
    *   **Sibling Embedding Synchronization**: Synchronize and link all sibling paper embeddings within a book to optimize intra-volume semantic relationships.
*   **Library Geo-Location Management**:
    *   **Interactive Coordinate Mapping**: Set or remove optional geographic coordinates (latitude and longitude) for the library using an interactive Leaflet map, device GPS, or manual input.
    *   **Community Circuit Discoverability**: Public libraries with configured coordinates can be found by nearby readers filtering by distance.
*   **AI-Assisted Cataloging**:
    *   **Google Books Integration**: Autofill metadata from a single ISBN or Title.
    *   **AI Description Synthesis**: Automatically generate high-quality summaries for rare or older books lacking digital records.
    *   **AI Cover Recognition**: Register books by taking a photo of the cover; AI resolves the title, authors, and metadata.
    *   **Narrative Undo**: A safety mechanism to revert AI-generated content if it doesn't meet archival standards.
*   **Archival Tagging System**: Categorize books using a multi-type tagging system (Book, Author, Person, General) with color-coded visual cues.
*   **Author Registry**: Manage a database of creators with automated **Wikipedia Portrait Discovery** and biographical link fetching.
*   **Bulk Operations**:
    *   **Mass Import via ISBN**: Upload lists of ISBNs for automated batch registration.
    *   **Bulk Metadata Editing**: Simultaneously update categories, years, or status for hundreds of records.
*   **Record Sealing**: Protect verified archival records from accidental modification by "Sealing" them.

### ⚙️ The Administrator
*Focused on system health, security, and compliance.*

*   **User & Role Management**: Granular control over library access, elevating users to Manager or Librarian roles.
*   **GDPR Compliance ("Right to be Forgotten")**: 
    *   **Ghosting Pattern**: Anonymize user PII (Email, Name, Fiscal Code) while preserving archival integrity of loan history.
*   **System Maintenance Panel**:
    *   **Vector DB Reconciliation**: Tools to sync the semantic search database with the event store, purging orphaned embeddings.
    *   **Embedding Integrity Checks**: Detect and fix books with missing or outdated semantic data.
*   **Advanced Security**:
    *   **Bot Protection**: Integrated reCAPTCHA and bot-score analysis to prevent automated scraping.
    *   **Audit Trail**: Every change in the system is an immutable event, providing a perfect history of all catalog and user actions.

---

## 3. Technical Excellence (Internal Architecture)

### 🏗️ Architecture: The "Sanctuary" Stack
*   **F# Functional Core**: The domain logic is written in F#, leveraging Discriminated Unions and Result types to make invalid states unrepresentable.
*   **Event Sourcing (Sharpino)**: Instead of storing just the "current state," the system stores the entire history of events. This allows for:
    *   **Perfect Auditing**: Know exactly who changed what and when.
    *   **Time Travel**: The ability to reconstruct the state of the library at any point in history.
    *   **Scalability**: Optimized read-models (Details) that are decoupled from the write-side.
*   **Blazor InteractiveServer UI**: A rich, responsive C# interface providing a "desktop-like" experience in the browser with real-time updates.

### 🤖 AI & Search Infrastructure
*   **Multi-Tier Semantic Vector Database**: Powered by **PostgreSQL + pgvector**, storing high-dimensional embeddings partitioned by item type (`book` descriptions and `paper` abstracts/texts).
*   **Large Language Model Integration**: Uses **Gemini 3.6 Flash** (and Vision) for metadata resolution, text synthesis, and match explanations.
*   **RAG Conversational Engine (AI Librarian)**: Orchestrates vector similarity retrieval with conversation memory, injecting relevant book/paper context into Gemini prompts and synthesizing cited, grounded answers.
*   **Geographical Proximity Engine**: Employs spherical coordinate distance calculations (Haversine formula) in F# domain services and interactive Leaflet map JS-interop to filter and order public circuits by user proximity.
*   **Match Explanations**: The AI doesn't just find a book; it can explain *why* it matches a semantic query.

### 📦 Infrastructure & Reliability
*   **PostgreSQL**: Solid, industrial-grade relational storage for both events and snapshots.
*   **Message Bus and Azure Sql Db (Optional)**: Support for high-performance distributed caching.
*   **Background Reliable Mailer**: A dedicated worker that retries failed notifications (e.g., loan reminders) to ensure delivery.

---

## 4. Future Roadmap
*   **Event Browser**: A low-level administrative tool to visualize the live stream of archival events.
*   **Automated Scheduling**: Background services for automated catalog purging and metadata synchronization.
*   **Social Archiving**: Collaborative tagging and community-driven review systems.
