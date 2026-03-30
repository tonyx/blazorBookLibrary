# Blazor Book Library - Project Documentation

This documentation provides an overview of the capabilities and structure of the **Blazor Book Library** application. It aligns the core domain actions (exposed via services) with the user interface components (Blazor Pages) that allow users to interact with the system.

## 1. Core Services & Domain Actions

The application relies heavily on domain-driven services to define the actions users, librarians, or system administrators can perform. These services encapsulate the specific business rules of the library:

### 1.1 Book Management (`IBookService`)
The book service is the primary access point for everything related to the library's catalog.
* **Core Actions:** Add, remove, and retrieve book information (`AddBookAsync`, `RemoveBookAsync`, `GetBookAsync`, `GetBookDetailsAsync`).
* **Metadata Actions:** Manage categories (`ChangeMainCategoryAsync`, `AddAdditionalCategoryAsync`, `RemoveAdditionalCategoryAsync`), update the title (`UpdateTitleAsync`), and attach/detach authors to books (`AddAuthorToBookAsync`, `RemoveAuthorFromBookAsync`).
* **State Management:** Books can be "sealed" or "unsealed" indicating finalization or locking of records (`SealAsync`, `UnsealAsync`).
* **Search Capabilities:** A highly robust searching mechanism allows querying by Title, ISBN, Publication Year, Authors, and Categories simultaneously. It also supports dynamic filtering (e.g., via `BookSearchCriteria` delegate) to restrict searches to specific states like "Immediately Available" (`SearchCriteria.searchImmediatelyAvailable`).

### 1.2 Author Management (`IAuthorService`)
Maintains the registry of authors whose books are in the library.
* **Core Actions:** Register new authors (`AddAuthorAsync`, `AddAuthorsAsync`), retrieve author details (`GetAuthorDetailsAsync`), and remove authors (`RemoveAsync`).
* **Metadata Actions:** Update an author's name (`RenameAsync`), specific identifier like ISNI (`UpdateIsniAsync`), and manage author pictures (`UpdateImageUrlAsync`, `RemoveImageUrlAsync`).
* **State Management:** Authors can similarly be sealed or unsealed (`SealAsync`, `UnsealAsync`).
* **Search Capabilities:** Search authors by Name, ISNI, or a combination of both.

### 1.3 Circulation & Access (`ILoanService` & `IReservationService`)
These services govern the lifecycle of borrowing physical items from the collection.
* **Loans:** Check out a book to a user (`AddLoanAsync`), retrieve loan details (`GetLoanAsync`), and process a return (`ReleaseLoanAsync`).
* **Reservations:** Allow a user to place a hold on a book (`AddReservationAsync`), fetch reservation details, and remove or fulfill a hold (`RemoveReservationAsync`).

### 1.4 User Management (`IUserService`)
Manages the library patrons and admin accounts within the sharpino domain context.
* **Core Actions:** Create new application users (`CreateUserAsync`) and look up existing users (`GetUserAsync`).

---

## 2. User Interface Components (Pages)

The web UI is built with Blazor and translates the aforementioned actions into structured user workflows. 

### 2.1 Public & Patron Interfaces
* **`BooksBrowser.razor`** 
  * **Purpose:** The main catalog search portal for patrons.
  * **Features:** Provides complex, combined search criteria matching the `IBookService` (Title, ISBN, exact/ranged Year, Category, Author selection). Includes responsive validation and a grid layout to view matching results. It recently implemented a global criteria switch to restrict results strictly to items that are "Available Only" (not currently on loan).
* **`BookDetails.razor`** 
  * **Purpose:** The dedicated view for a single book.
  * **Features:** Displays rich metadata, its status (Available/On Loan), assigned categories, and associated authors. Used to trigger reservations or detail checks.
* **`AuthorsDetails.razor`** 
  * **Purpose:** The dedicated view for a single author.
  * **Features:** Shows the author's biography, ISNI identifier, image, and a mapped list of books bound to them within the catalog.

### 2.2 Librarian & Admin Interfaces
* **`BooksManager.razor`** 
  * **Purpose:** An administrative dashboard to curate book records.
  * **Features:** Inherits all the complex search queries from `BooksBrowser`. Additionally, it implements a dynamic "Add New Book Entry" form. This workflow integrates with the `IGoogleBooksService` to search an external API and autofill standard metadata (ISBN, Year, Title). It contains embedded sub-components to seamlessly search for and attach `Author` entities to a `Book` in a single pass before submission.
* **`AuthorsManager.razor`** 
  * **Purpose:** An administrative dashboard purely for authorizing and maintaining author entities.
  * **Features:** Allows creating new authors manually, verifying ISNIs, uploading author portrait URLs, and fixing typos in author names.
* **`UsersManager.razor`** 
  * **Purpose:** Simple admin portal to register and lookup the system's users/patrons interacting with the `IUserService`.

---

## 3. Architecture & Patterns

* **Domain-Driven Design (CQRS / Event Sourcing):** Features like `StateView.getAllFilteredAggregateStatesAsync` and sealing/unsealing capabilities strongly indicate an events-based architecture (using Sharpino) where the specific actions append events to aggregate logs.
* **Functional Core / Imperative Shell:** The backend is deeply rooted in F# (leveraging `Result`, `Option`, and `TaskResult`), cleanly mapping domain rules into explicit service boundaries. At the same time, the Blazor front-end relies on standard interactive DI (dependency injection) injection patterns (e.g. `[Inject] IBookService`) to interface with this underlying functional domain. 
* **Strong Typing:** Types such as `Title`, `Isbn`, `Isni`, and `Name` are domain primitives enforcing validation (e.g. `Isbn.NewInvalid()` ensuring syntax checks before an entry is made).
