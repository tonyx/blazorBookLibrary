# User Stories: Blazor Book Library

This document outlines the core functionality of the Blazor Book Library application from the perspective of different user roles, based on an analysis of the domain commands, services, and UI components.

## Roles
- **Visitor**: An unauthenticated user browsing the library.
- **Member**: A registered user who can borrow and reserve books.
- **Librarian/Manager**: A staff member responsible for updating the book catalog and managing authors.
- **Administrator**: A system administrator with full access, including user and role management.

---

## 1. Book Discovery & Search
### **As a Visitor or Member, I want to search for books by title, ISBN, year, or category so that I can find literature that interests me.**
- **Criteria**: 
    - Search by full or partial title.
    - Search by exact ISBN or partial ISBN string.
    - Search by publication year (exact, before, after, or within a range).
    - Filter results by main or additional categories.
    - Combine multiple search criteria (e.g., Title + Year).
    - Results should display book details, categories, and real-time availability.

### **As a Visitor or Member, I want to view detailed information about a specific book, including its description and authors.**
- **Criteria**:
    - View book title, authors, publication year, and categories.
    - Read a detailed description of the book (if available).
    - See the book's cover image.
    - Check current loan status and expected return dates.
    - See future reservations to plan my own borrowing.

---

## 2. Catalog Management (Librarians & Admins)
### **As a Librarian, I want to add new books to the library catalog efficiently.**
- **Criteria**:
    - Manually enter book details (Title, ISBN, Year, Categories).
    - Use a barcode scanner (via camera) to quickly capture ISBNs.
    - Bulk add books or use external APIs (Google Books) to autofill metadata and cover images.
    - Associate multiple authors with a book during creation.

### **As a Librarian, I want to manage and update existing book information to keep the catalog accurate.**
- **Criteria**:
    - Update title, description, ISBN, and publication year.
    - Change the main category or manage multiple additional categories.
    - Add or remove authors and translators from a book record.
    - Update or remove the book's cover image URL.
    - Set the book's base availability type (Circulating vs. Reference Only).

### **As a Librarian, I want to perform bulk updates on multiple books to save time on repetitive tasks.**
- **Criteria**:
    - Select multiple books from search results.
    - Simultaneously update the year, main category, additional categories, or availability status for all selected books.

### **As a Librarian, I want to delete books from the catalog when they are no longer part of the library collection.**
- **Criteria**:
    - Remove a book record permanently.
    - Prevention: Prevent deletion if the book currently has active loans or pending reservations.

### **As a Librarian, I want to "seal" or "unseal" a book record to control its editability.**
- **Criteria**:
    - Seal a book to prevent accidental edits or during specific administrative phases.
    - Unseal a book when updates are required.

---

## 3. Author Management (Librarians & Admins)
### **As a Librarian, I want to manage the library's database of authors.**
- **Criteria**:
    - Create new author profiles with names and ISNI (International Standard Name Identifier) codes.
    - Search for authors by name or ISNI.
    - Update author details (name, ISNI, image URL).
    - View all books associated with a specific author.
    - Remove authors who no longer have any books associated with them.

---

## 4. Lending & Reservations (Members & Librarians)
### **As a Member, I want to return a borrowed book so that it becomes available for others.**
- **Criteria**:
    - Trigger a "Return" command on an active loan (Domain logic supported).

### **As a Member or Librarian, I want to cancel a reservation if I no longer need the book.**
- **Criteria**:
    - Members can cancel their own reservations.
    - Librarians can cancel any reservation on behalf of a user.

---

## 5. User & System Management (Admins Only)
### **As an Administrator, I want to manage user accounts and assign roles.**
- **Criteria**:
    - Search for users by email, name, or username.
    - Promote a standard user to the "Manager" role to grant them catalog management permissions.
    - Revoke the "Manager" role to return a user to standard member status.
    - View a user's current roles and basic account information.
