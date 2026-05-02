# End-User Manual: Blazor Book Library

Welcome to the **Blazor Book Library** manual. This guide will help you navigate the system, manage the catalog, and understand the core features of the platform.

---

## Table of Contents
1. [Introduction](#introduction)
2. [Getting Started](#getting-started)
3. [Core Features for All Users](#core-features-for-all-users)
    - [Search & Discovery](#search--discovery)
    - [Viewing Book Details](#viewing-book-details)
4. [Member Privileges](#member-privileges)
    - [Loans & Returns](#loans--returns)
    - [Reservations](#reservations)
5. [Librarian & Manager Tools](#librarian--manager-tools)
    - [Catalog Management](#catalog-management)
    - [Barcode Scanning](#barcode-scanning)
    - [Author Registry](#author-registry)
    - [Bulk Operations](#bulk-operations)
6. [System Administration](#system-administration)
    - [User Management](#user-management)
    - [Email Service Reliability](#email-service-reliability)
7. [Troubleshooting & Support](#troubleshooting--support)

---

<a name="introduction"></a>
## 1. Introduction
The Blazor Book Library is a modern, high-performance archival system designed for discoverability and ease of use. It leverages event-sourcing for a perfect audit trail and integrates with global services like Google Books to keep your catalog rich and accurate.

<a name="getting-started"></a>
## 2. Getting Started
### Login and Authentication
- Access the system via the **Login** link in the navigation menu.
- You can register a new account or sign in with your existing credentials.
- The system supports **Social Login** (e.g., Google OAuth) for a seamless experience.

### Main Dashboard
- Once logged in, the **Home** page provides a quick overview of the library's recent additions.
- Use the sidebar or top navigation to jump to **Library Search**, **Books Manager**, or **Authors Manager** (depending on your role).

---

<a name="core-features-for-all-users"></a>
## 3. Core Features for All Users

<a name="search--discovery"></a>
### Search & Discovery
The **Library Search** page is your primary tool for finding literature.
- **Title Search**: Enter any part of a book title.
- **ISBN Search**: Locate a book specifically by its 10 or 13-digit ISBN.
- **Advanced Filters**:
    - **Authors**: Filter by one or more authors.
    - **Categories**: Filter by genre or classification (e.g., Fiction, Science, History).
    - **Timeline**: Search for books published in a specific year or within a range.
    - **Availability**: Filter to show only books that are immediately available for loan.
- **AI Semantic Discovery**:
    - Enter a descriptive phrase (e.g., "a dystopian novel about surveillance") to find books with similar meanings, even if they don't contain the exact keywords searched.
    - Specify the maximum number of results desired to fine-tune your discovery.

<a name="viewing-book-details"></a>
### Viewing Book Details
Clicking on a book title opens the **Book View** page.
- **Overview**: View cover images, summaries, and metadata.
- **Availability Status**: See at a glance if the book is on the shelf, on loan, or reference-only.
- **Return Dates**: If on loan, the expected return date is displayed.

---

<a name="member-privileges"></a>
## 4. Member Privileges

<a name="loans--returns"></a>
### Loans & Returns
Members can manage their own borrowings.
- **Borrowing**: Navigate to a book detail page and click **"Borrow"** if the book is circulating and available.
- **Returns**: Active loans can be finalized by clicking **"Return"**, making the book available for others.

<a name="reservations"></a>
### Reservations
If a book is currently on loan, you can place a **Reservation**.
- You will be notified when the book is returned and reserved for you.
- Reservations can be canceled at any time from your profile or the book page.

---

<a name="librarian--manager-tools"></a>
## 5. Librarian & Manager Tools

<a name="catalog-management"></a>
### Catalog Management
Managers use the **Books Manager** to maintain the library's excellence.
- **Adding Books**: Click **"Add New Book Entry"** to open the registration form.
- **Google Books Integration**: Enter a title and click **"Search API"** to automatically pull metadata (description, year, authors) from global records.
- **Editing Records**: Click any title in the manager list to modify its details, including categories and archival notes.
- **AI-Powered Enrichment**:
    - **Generate Missing Descriptions**: If a book lacks a summary, use the **"Generate Description"** tool. The system leverages AI to synthesize a high-quality abstract based on the title and existing metadata.
    - **Narrative Undo**: If you're not satisfied with the AI-generated text, use the **"Undo"** button to revert to the previous version instantly.
- **AI Embedding Management**:
    - To enable semantic search, every book must have an "embedding" (vector data) associated with its description.
    - In the book edit page, if vector data is missing, click **"Generate Embedding"**. The system will use AI to convert the description into searchable vector data.
    - **Sanity Check**: Once generated, you can perform a **"Sanity Check"** by entering text similar to the description and verifying that the book correctly appears at the top of the semantic search results.
    - You can remove or update the embedding at any time, which is recommended after significantly updating the book's description.

<a name="barcode-scanning"></a>
### Barcode Scanning
The system supports physical hardware or camera-based scanning.
- In the Add Book form, click the **"Scan"** camera icon.
- Position the book's barcode in the frame. The system will capture the ISBN and allow you to **"Autofill"** metadata immediately.

<a name="author-registry"></a>
### Author Registry
Manage the database of creators in the **Authors Manager**.
- **Wikipedia Lookup**: When adding an author, use the **"Discover Portrait"** tool to automatically fetch a profile image and biographical link from Wikipedia.
- **Sealing Records**: Author profiles can be **Sealed** (locked) to prevent accidental changes during administrative reviews.

<a name="bulk-operations"></a>
### Bulk Operations
Save time with mass updates:
- Select multiple books in the **Books Manager** ledger.
- Click **"Bulk Edit"** to simultaneously update publication years, categories, or availability status for the entire selection.

<a name="import--export"></a>
### Archival Import & Export
The **Books Manager** provides powerful tools for mass data handling.
- **Exporting the Archive**: Use the **"Export"** dropdown to download your entire library catalog in **JSON** or **CSV** formats. This is ideal for backups or external analysis.
- **ISBN Bulk Import**: 
    - Click **"Import"** to open the bulk registration tool.
    - Paste a list of ISBNs (one per line) or upload a text file.
    - **Configuration Options**:
        - **Generate missing authors**: Automatically create author profiles for unrecognized creators.
        - **Allow duplicate ISBNs**: Enable this to permit multiple entries of the same ISBN.
        - **Generate missing descriptions**: Use AI to synthesize summaries for books where global APIs provide none.
        - **Generate automatically embedding**: Instantly prepare the book for semantic search upon import.
    - **Intelligent Resolution**: The system automatically looks up metadata for each ISBN via global APIs and AI services.
    - **Progress Tracking**: A real-time progress bar shows the current status and estimated time remaining for large imports.
    - **Post-Import Summary**: Upon completion, a detailed report displays successful imports, warnings, and errors. You can easily copy the ISBNs of failed records to retry them later.

---

<a name="system-administration"></a>
## 6. System Administration

<a name="user-management"></a>
### User Management
Administrators manage access via the **Users Manager**.
- **Role Assignment**: Promote users to **Manager** or **Librarian** roles to grant catalog access.
- **Account Control**: Search for users by email or username to review their status and roles.

### GDPR Anonymization & "Right to be Forgotten"
Users can request account deletion via their profile settings (**Manage Your Data**). 
- **Anonymization Flow**: To comply with GDPR while preserving the library's historical records (like past loans and reviews), the system performs **Anonymization** instead of hard deletion.
- **Account Disabling**: The user's personal details (Email, Name, Fiscal Code) are permanently cleared and replaced with randomized identifiers. The account is then permanently locked.
- **Record Preservation**: All historical interactions (e.g., that a book was borrowed by *User X*) remain intact for archival integrity, but *User X* can no longer be identified.

<a name="email-service-reliability"></a>
### Email Service Reliability
The system includes a dedicated **Background Mail Resender Service**.
- If a notification email (like account confirmation or loan alerts) fails due to external service issues, the system automatically queues it.
- A background worker retries failed emails every 10 minutes until successful, ensuring no critical communication is lost.

---

<a name="troubleshooting--support"></a>
## 7. Troubleshooting & Support

| Issue | Potential Solution |
| :--- | :--- |
| **Email Not Received** | Check your Spam folder. If it still hasn't arrived, the system's background worker will retry the delivery automatically. |
| **Scan Failed** | Ensure adequate lighting and that the barcode is clean. Alternatively, type the ISBN manually and use the **Lookup** button. |
| **Cannot Edit Record** | Check if the record is **"Sealed"**. A Manager or Admin must unseal it before updates can be applied. |
| **Google Books No Results** | Verify the title spelling or try searching by ISBN for more precise matching. |

---
*For technical architecture details, see [Architecture.md](file:///Users/antoniolucca/github/blazorBookLibrary/Docs/Architecture.md).*
