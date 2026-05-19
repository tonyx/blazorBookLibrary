# End-User Manual: Blazor Book Library

Welcome to the **Blazor Book Library** manual. This guide will help you navigate the system, understand the multi-tenancy concept, manage catalogs, and explore the advanced features of the platform.

---

## Table of Contents
1. [Introduction](#introduction)
2. [Getting Started & Multi-Tenancy](#getting-started--multi-tenancy)
    - [The Multi-Tenant (Circuits) Architecture](#the-multi-tenant-circuits-architecture)
    - [Login and Authentication](#login-and-authentication)
    - [Active Context & Circuit Switching](#active-context--circuit-switching)
3. [Core Features for All Users](#core-features-for-all-users)
    - [Search & Discovery](#search--discovery)
    - [Viewing Book Details](#viewing-book-details)
4. [Patron Privileges & Circulation](#patron-privileges--circulation)
    - [Accepting a Circuit Invitation](#accepting-a-circuit-invitation)
    - [Loans & Returns (Circulation Rules)](#loans--returns-circulation-rules)
    - [Reservations](#reservations)
5. [Tenant Owner & Manager Tools](#tenant-owner--manager-tools)
    - [Creating Your Own Tenant (Library-as-a-Service)](#creating-your-own-tenant-library-as-a-service)
    - [Tenant Visibility & Settings (Public vs. Private)](#tenant-visibility--settings-public-vs-private)
    - [Patron Roster & Role Delegation](#patron-roster--role-delegation)
    - [Managing Distribution Points & Custody](#managing-distribution-points--custody)
    - [Catalog Management](#catalog-management)
    - [AI-Powered Enrichment & Embeddings](#ai-powered-enrichment--embeddings)
    - [Archival Import & Export](#archival-import--export)
6. [System Administration](#system-administration)
    - [User Management](#user-management)
    - [Admin Control Panel & Reconciliation](#admin-control-panel--reconciliation)
    - [GDPR Anonymization & "Right to be Forgotten"](#gdpr-anonymization--right-to-be-forgotten)
    - [Email Service Reliability](#email-service-reliability)
7. [Troubleshooting & Support](#troubleshooting--support)

---

<a name="introduction"></a>
## 1. Introduction
The Blazor Book Library is a modern, high-performance archival and lending system. It is powered by an event-sourced architecture and leverages advanced AI integrations (such as semantic search and cover recognition) to make cataloging and discovering books seamless. 

Rather than utilizing a single shared database, the platform organizes books, loans, and members into isolated lending ecosystems called **Circuits** (or **Tenants**). This guides you through the standard library experience and the process of running your own independent digital library service.

---

<a name="getting-started--multi-tenancy"></a>
## 2. Getting Started & Multi-Tenancy

<a name="the-multi-tenant-circuits-architecture"></a>
### The Multi-Tenant (Circuits) Architecture
To serve different communities and book collectors, the system runs on a self-governing multi-tenant model:
1. **The Demonstration Circuit (Default Tenant)**: Upon registration, all users immediately join and browse the **Default Tenant**. This acts as a common sandbox containing a predefined public catalog, common categories, and sample distribution points, allowing zero-friction exploration.
2. **Autonomous Circuits (Library-as-a-Service)**: Any registered user can instantly spin up a completely isolated **Tenant** at any time. As the **Owner**, you possess absolute control over this custom circuit—meaning you can catalog your own books, invite specific friends or neighbors, assign roles, and decide access policies without affecting anyone else.

<a name="login-and-authentication"></a>
### Login and Authentication
- Access the system via the **Login** link in the navigation menu.
- You can register a new account or sign in with your credentials.
- The system supports **Social Login** (e.g., Google OAuth), **Passkey Authentication**, and **Two-Factor Authentication (2FA)** for secure, modern access.

<a name="circuit-switching"></a>
### Active Context & Circuit Switching
- Your active circuit determines what you see. Every catalog browse, semantic search, distribution point check, or loan transaction operates strictly within the boundaries of your **Active Tenant**.
- Switch contexts easily by using the **Tenant Selector** dropdown located in the navigation header. Select any circuit you own or belong to as a patron to immediately transition your active workspace context.

---

<a name="core-features-for-all-users"></a>
## 3. Core Features for All Users

<a name="search--discovery"></a>
### Search & Discovery
The **Library Search** page is designed to query the active tenant's catalog:
- **Title Search**: Enter any part of a book title.
- **ISBN Search**: Locate a book specifically by its 10 or 13-digit ISBN.
- **Advanced Filters**: Filter by one or more authors, genres/categories, publication timeline ranges, or show only immediately available items.
- **AI Semantic Discovery**: 
    - Enter natural language queries (e.g., "a dystopian story about memory loss and totalitarian control") to fetch books with semantically related topics, even if they don't share exact keywords.
    - Limit the returned results count to refine your discovery lists.

<a name="viewing-book-details"></a>
### Viewing Book Details
Clicking a book opens the **Book View** page:
- **Overview**: View cover images, abstracts, and categories.
- **Location & Custody**: Check which physical **Distribution Point** currently holds the book.
- **Availability Status**: See if the book is on the shelf, on loan (with expected return date), or designated as reference-only.

---

<a name="patron-privileges--circulation"></a>
## 4. Patron Privileges & Circulation

<a name="accepting-a-circuit-invitation"></a>
### Accepting a Circuit Invitation
If a friend, family member, or community leader invites you to their private tenant, you will receive an invitation email containing a secure link.
1. Click the dynamic link in the invitation email.
2. Log in or create a new account if you haven't already.
3. The system converts your invitation into a registered **Patron** role inside the host tenant, sets your active tenant context, and saves a secure cookie (`selected_tenant`).
4. You are redirected to the new personalized dashboard to begin browsing their collection immediately.

<a name="loans--returns-circulation-rules"></a>
### Loans & Returns (Circulation Rules)
- **Borrowing**: Navigate to an available book's details page and click **"Borrow"**. You can select a physical **Distribution Point** to coordinate the pickup.
- **Returns (Strict Constraint)**: To preserve physical inventory tracking, **books must be returned to the exact physical Distribution Point** from which they were borrowed or registered.
- **Self-Service vs. Custodian Approval**:
  - In a **Custodian-Led** tenant, a designated caretaker (Reference User) must physically check and approve the pickup or return in the system.
  - In a **Self-Service (Trust-Based)** tenant, patrons directly confirm their physical pickups and returns digitally, relying entirely on mutual trust and honesty.

<a name="reservations"></a>
### Reservations
If a book is currently on loan, click **"Place Reservation"** to queue for the copy. You will be notified via email when it is returned. You can cancel your reservations at any time from your profile dashboard.

---

<a name="tenant-owner--manager-tools"></a>
## 5. Tenant Owner & Manager Tools

<a name="creating-your-own-tenant-library-as-a-service"></a>
### Creating Your Own Tenant (Library-as-a-Service)
To host your own sharing circuit:
1. Navigate to the **Tenant Dashboard** and select **"Create New Circuit"**.
2. Give your circuit a unique name.
3. Upon creation, you are registered as the **Owner** of the new tenant context, granting you absolute administrative control.

<a name="tenant-visibility--settings-public-vs-private"></a>
### Tenant Visibility & Settings (Public vs. Private)
Owners can toggle their tenant's privacy configuration:
- **🔒 Private (Closed Circle)**: Virtually invisible. The catalog and details are completely hidden from general platform users. Users must be invited via email to browse or borrow. This is perfect for family shelves, book clubs, or small circles of friends.
- **🏛️ Public (Community Library)**: Searchable and browseable by any registered platform user. Ideal for community workspaces, neighborhood cafes, public micro-libraries, and bookcrossing networks.

<a name="patron-roster--role-delegation"></a>
### Patron Roster & Role Delegation
As a Tenant Owner:
- **Onboarding Patrons**: Enter a user's email under **"Invite Patrons"** to send an email invitation. You can view all pending invites and revoke them if necessary.
- **Assigning Managers**: Promote active Patrons to the **Manager** role, allowing them to manage books, authors, categories, and generate AI embeddings.
- **Demoting / Revoking**: Demote managers back to patrons or remove inactive members from your tenant roster completely.

<a name="managing-distribution-points--custody"></a>
### Managing Distribution Points & Custody
Define the physical infrastructure where books reside (e.g., "Living Room Shelf", "Office Cabinet", "Green Cafe Box"):
1. Create new **Distribution Points** inside your tenant.
2. Choose a **Lending Model**:
   - **Self-Service**: Turn off reference verification. Users borrow/return books without oversight.
   - **Delegated Custody**: Assign one or more patrons as **Reference Users** for specific locations. These individuals must approve physical actions in the system.

<a name="catalog-management"></a>
### Catalog Management
Managers and Owners utilize the **Books Manager** ledger:
- **Add New Book**: Enter details manually or scan a physical book's barcode using your device's camera to pull metadata from Google Books.
- **AI Cover Recognition**: If a barcode is damaged or missing, click the camera icon, take a clear photo of the cover, and let our advanced AI vision model resolve the title, authors, and metadata.
- **Author Registry**: Manage creators, lookup profile biographies, and import portraits directly from Wikipedia. You can **"Seal"** author profiles to freeze them against accidental modifications.

<a name="ai-powered-enrichment--embeddings"></a>
### AI-Powered Enrichment & Embeddings
- **Generate Abstracts**: Click **"Generate Description"** to let the AI write a comprehensive summary based on title metadata. You can "Undo" the generation if needed.
- **Semantic Search Vector Embeddings**: To enable AI semantic search, click **"Generate Embedding"** on a book edit page. The AI converts descriptions into mathematical vectors.
- **Sanity Check**: Test the accuracy of your semantic indices by typing natural-language queries directly into the book validation panel to see where it ranks.

<a name="archival-import--export"></a>
### Archival Import & Export
- **Export**: Instantly download the active tenant's catalog as **CSV** or **JSON** for digital backups or legacy printing.
- **Bulk ISBN Import**: Paste a list of ISBNs (one per line) or upload a text file. Customize options to automatically fetch metadata, generate missing descriptions, auto-create author profiles, and build semantic embeddings on-the-fly with a real-time progress monitor.

---

<a name="system-administration"></a>
## 6. System Administration

<a name="user-management"></a>
### User Management
Global Administrators oversee platform safety and system-wide roles via the **Users Manager**, searching profiles, reviewing global status, and assigning platform-level manager roles.

<a name="admin-control-panel--reconciliation"></a>
### Admin Control Panel & Reconciliation
Provides platform-level integrity checks:
- **Purge Orphan Vectors**: Removes obsolete search embeddings not tied to existing books.
- **Sync Book States**: Corrects discrepancy where books reference invalid vector store pointers.

<a name="gdpr-anonymization--right-to-be-forgotten"></a>
### GDPR Anonymization & "Right to be Forgotten"
Members can trigger anonymization under **"Manage Your Data"**. To maintain catalog and lending history integrity while complying with privacy legislation:
- Personal information (Email, Name, Fiscal Code) is entirely replaced with randomized data.
- The user account is permanently deactivated.
- Historical loan interactions are preserved as anonymized "ghost" transactions for record integrity.

<a name="email-service-reliability"></a>
### Email Service Reliability
A dedicated background worker processes queued notification deliveries (e.g., invites, confirmations), retrying failed emails every 10 minutes to guarantee reliability.

---

<a name="troubleshooting--support"></a>
## 7. Troubleshooting & Support

| Issue | Potential Solution |
| :--- | :--- |
| **Email Not Received** | Check Spam/Junk. If missing, the background worker will retry within 10 minutes. |
| **Cannot View Invited Catalog** | Ensure you have accepted the invitation link and that your active workspace is switched to the new tenant. |
| **Scan/Cover Capture Fails** | Ensure clean lighting and flat placement. If failing, input the ISBN manually for autofill. |
| **Record Is Locked** | Check if the book or author is "Sealed". An Owner, Manager, or Admin must unseal it before updates are allowed. |
| **Circulation Return Blocked** | Books must be returned to the specific Distribution Point they are registered at. Select the correct return destination. |

---
*For technical architecture details, see [Architecture.md](file:///Users/antoniolucca/github/blazorBookLibrary/Docs/Architecture.md).*
