# User Stories: Blazor Book Library

This document outlines the core functionality of the Blazor Book Library application from the perspective of different user roles, updated to reflect the self-governing **Multi-Tenant (Circuit)** architecture.

## Roles
- **Visitor**: An unauthenticated user browsing public catalogs or public demonstration areas.
- **Member (Patron)**: A registered user belonging to one or more tenants (by default, the demonstration tenant, or any autonomous tenant they have joined). Patrons can browse catalogs, borrow books, and make reservations within their active tenant context.
- **Owner**: An authenticated user who creates a new autonomous tenant (circuit) to run a custom library service. Owners have absolute administrative authority to set privacy (Public vs. Private), manage patron rosters, invite new patrons, and delegate roles.
- **Librarian / Manager**: A staff member or tenant-appointed role responsible for catalog operations (adding/editing books, managing authors, generating AI embeddings) within a specific tenant context.
- **Reference User (Custodian)**: A tenant member delegated by the Owner or Manager to act as a custodian for a specific physical Distribution Point, responsible for verifying book handovers and returns.
- **Administrator**: A system-wide administrator with full access to global user management, service auditing, and database reconciliation tools.

---

## 1. Book Discovery & Search (Scoped to Active Tenant)
### **As a Visitor or Patron, I want to search for books by title, ISBN, year, or category so that I can find literature that interests me in the active catalog.**
- **Criteria**: 
    - Search by full or partial title.
    - Search by exact ISBN or partial ISBN string.
    - Search by publication year (exact, before, after, or within a range).
    - Filter results by main or additional categories defined in the active tenant.
    - Combine multiple search criteria (e.g., Title + Year).
    - Results should display book details, categories, and real-time availability within the current active tenant context.

### **As a Visitor or Patron, I want to search for books by their meaning or topic (AI Semantic Search) so that I can discover relevant literature even without knowing exact titles or keywords.**
- **Criteria**:
    - Enter a natural language query describing a topic or plot (e.g., "stories about robots learning to love").
    - Specify the maximum number of results (limit) to return.
    - Results are ordered by semantic similarity to the query, pulled from the active tenant's vector database records.
    - Semantic search can be combined with other filters (like categories or authors) to narrow down AI-driven discoveries within the tenant context.

### **As a Visitor or Patron, I want to view detailed information about a specific book, including its description, location, and authors.**
- **Criteria**:
    - View book title, authors, publication year, and categories.
    - Read a detailed description of the book (if available).
    - See the book's cover image.
    - Check current loan status, active borrower (if permitted), and expected return dates.
    - See which physical Distribution Point the book belongs to.
    - See future reservations to plan my own borrowing.

---

## 2. Multi-Tenancy & Circuit Administration (Owners & Patrons)
### **As an Authenticated User, I want to create a new autonomous Tenant (Circuit) so that I can host my own private or public book catalogue.**
- **Criteria**:
    - Choose a unique name for my new Tenant.
    - The system instantly spins up the isolated Tenant and designates me as its absolute **Owner**.
    - The new Tenant gets a clean, independent universe with no books or distribution points, ready for setup.

### **As a Tenant Owner, I want to toggle the visibility of my Tenant between Public and Private so that I can control who can discover my book catalogue.**
- **Criteria**:
    - A **Public** tenant is searchable and discoverable by any platform visitor or member, enabling open community book sharing.
    - A **Private** tenant is completely hidden from public directories, accessible only to invited patrons.
    - Toggle visibility dynamically via the Tenant management page.

### **As a Tenant Owner, I want to view my Tenant details page with configuration options, and ensure that only I (the Owner) can access these administrative controls.**
- **Criteria**:
    - Access a dedicated details dashboard for my tenant when navigating as the Owner.
    - Conditionally render administrative controls (like setting privacy or sending invitations) so they are hidden from standard patrons or non-owner viewers.

### **As a Tenant Owner or Manager, I want to invite new Patrons to my circuit via email so that they can join my library.**
- **Criteria**:
    - Enter the email address of the person I want to invite.
    - The system generates a secure, unique `PatronInvitationCode` and sends an invitation link containing the specific `tenantId` and invitation code.
    - View a list of outstanding invitations with their status (invited, accepted, etc.).
    - Revoke an invitation if necessary before it is accepted.

### **As an Invitee, I want to accept an invitation link so that I can easily register and onboard into the host tenant.**
- **Criteria**:
    - Click a dynamic, host-independent invitation link received via email.
    - Complete registration or sign in to authenticate.
    - The system automatically associates my user account as a **Patron** of the target tenant.
    - Automatically switch my active context to the new tenant, setting secure session cookies (`selected_tenant`), and redirecting me to the personalized dashboard of that tenant.

### **As a Patron, I want to switch my active context between the Default Tenant and any autonomous tenants I belong to so that I can browse different catalogs.**
- **Criteria**:
    - View a dropdown or selector showing all the circuits I own or am registered in as a patron.
    - Select a tenant to instantly switch my context and browse its specific books, authors, and distribution points.

---

## 3. Catalog Management (Librarians, Managers & Owners)
### **As a Librarian, I want to add new books to my active tenant's catalog efficiently.**
- **Criteria**:
    - Manually enter book details (Title, ISBN, Year, Categories).
    - Use a barcode scanner (via camera) to quickly capture ISBNs.
    - Bulk add books or use external APIs (Google Books) to autofill metadata and cover images.
    - Associate multiple authors with a book during creation.
    - Assign the book to a specific physical Distribution Point within the tenant.

### **As a Librarian, I want to manage and update existing book information to keep the catalog accurate.**
- **Criteria**:
    - Update title, description, ISBN, and publication year.
    - Change the main category or manage multiple additional categories.
    - Add or remove authors and translators from a book record.
    - Update or remove the book's cover image URL.
    - Set the book's base availability type (Circulating vs. Reference Only).
    - Transfer books between different Distribution Points inside the tenant context.

### **As a Manager, I want to manage AI embeddings for book descriptions so that I can ensure the accuracy and discoverability of my tenant's semantic search.**
- **Criteria**:
    - Identify books in my tenant context that are missing vector data (embeddings) for their descriptions.
    - Generate a new embedding for a book's description using an AI service.
    - Perform a "Sanity Check" by testing if the book is correctly retrieved by a query similar to its own description.
    - Remove or update an embedding manually when a book's description changes.
    - The system automatically handles embedding cleanup when a book is deleted.

### **As a Librarian, I want to perform bulk updates on multiple books to save time on repetitive tasks.**
- **Criteria**:
    - Select multiple books from search results inside the tenant context.
    - Simultaneously update the year, main category, additional categories, or availability status for all selected books.

### **As a Librarian, I want to delete books from the catalog when they are no longer part of the collection.**
- **Criteria**:
    - Remove a book record permanently.
    - Prevention: Prevent deletion if the book currently has active loans or pending reservations.

### **As a Librarian, I want to "seal" or "unseal" a book record to control its editability.**
- **Criteria**:
    - Seal a book to prevent accidental edits or during specific administrative phases.
    - Unseal a book when updates are required.

### **As a Manager, I want to export my tenant's catalog in JSON or CSV format so that I have a portable backup of the archival data.**
- **Criteria**:
    - Trigger an export from the Books Manager ledger.
    - Choose between JSON and CSV formats.
    - The downloaded file contains all relevant book metadata for the tenant.

### **As a Manager, I want to bulk import books using a list of ISBNs so that I can quickly populate my custom catalog.**
- **Criteria**:
    - Provide a list of ISBNs via text input or file upload.
    - The system resolves metadata (Title, Authors, Cover) for each valid ISBN automatically and creates the entries in the active tenant.
    - Real-time progress reporting should be visible during the import.

---

## 4. Author Management (Librarians, Managers & Owners)
### **As a Librarian, I want to manage the active tenant's database of authors.**
- **Criteria**:
    - Create new author profiles with names and ISNI (International Standard Name Identifier) codes.
    - Search for authors by name or ISNI.
    - Update author details (name, ISNI, image URL).
    - View all books associated with a specific author within the tenant context.
    - Remove authors who no longer have any books associated with them.

---

## 5. Lending & Reservations (Members & Reference Users)
### **As a Member, I want to borrow a book from a specific Distribution Point within my active tenant context.**
- **Criteria**:
    - Select an available book and place it on loan.
    - Choose the specific physical Distribution Point from which I will pick it up.

### **As a Member, I want to return a borrowed book so that it becomes available for others.**
- **Criteria**:
    - Return the book to the exact physical Distribution Point from which it was borrowed (or to which it is registered) to maintain physical inventory integrity.
    - Trigger a "Return" command on an active loan.

### **As a Reference User (Custodian), I want to verify a book's physical pickup or return so that the digital status matches reality.**
- **Criteria**:
    - View active loan transitions assigned to my designated Distribution Point.
    - Approve the pickup or return after inspecting the physical book condition.
    - Alternatively, in a self-service trust-based circuit, allow patrons to directly complete their loans and returns without custodian verification.

### **As a Member or Librarian, I want to cancel a reservation if I no longer need the book.**
- **Criteria**:
    - Members can cancel their own reservations.
    - Librarians can cancel any reservation on behalf of a user in their tenant context.

---

## 6. User & Role Management (Tenant Owners & Global Admins)
### **As a Tenant Owner, I want to manage my circuit's user accounts and assign roles.**
- **Criteria**:
    - View all Patrons currently registered to my tenant.
    - Promote a Patron to the "Manager" role to delegate catalog operations.
    - Demote a Manager back to Patron status.
    - Revoke a Patron's membership from my tenant context.

### **As an Administrator, I want to manage global user accounts and assign platform-level roles.**
- **Criteria**:
    - Search for users across the entire system.
    - View a user's registered tenants and basic account information.

---

## 7. Data Privacy & GDPR (Members)
### **As a Member, I want to exercise my "Right to be Forgotten" by anonymizing my account while ensuring the library's data integrity remains intact.**
- **Criteria**:
    - Request account anonymization from the user profile settings.
    - Personal identifiable information (Email, Name, Fiscal Code) is cleared and randomized across the platform and all tenants.
    - All existing connections to borrowed books or reviews remain as anonymized "ghost" records for historical accuracy.
    - The account is permanently disabled and cannot be used for future logins.
