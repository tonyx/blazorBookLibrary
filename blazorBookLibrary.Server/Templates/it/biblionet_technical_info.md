# Architettura e Tecnologie di BiblioNet.eu

BiblioNet.eu è una piattaforma moderna dedicata alla catalogazione, scoperta e fruizione del patrimonio bibliotecario e documentale.

A differenza dei tradizionali sistemi di gestione basati su database a stato mutabile (CRUD), BiblioNet.eu è progettato secondo i principi della **programmazione funzionale**, dell'**Event Sourcing** e dell'**interoperabilità aperta**. L'intero sistema è costruito per garantire tracciabilità storica assoluta, scalabilità e rigore transazionale.

---

## 1. Paradigma Architetturale: Event Sourcing & CQRS

Al cuore di BiblioNet.eu risiede il modello **Event Sourcing** integrato con il pattern **CQRS** (Command Query Responsibility Segregation). 

Nessuna riga del catalogo o dello stato operativo viene sovrascritta o cancellata in modo distruttivo. Ogni modifica nel ciclo di vita dell'applicazione è formalizzata come un fatto immutabile verificatosi nel tempo:

- **Command:** l'intenzione di eseguire un'azione (es. `CatalogBook`, `RecordLoan`, `AddSubjectHeading`). Viene validata formalmente rispetto allo stato corrente.
- **Event:** il risultato immutabile dell'operazione registrato in sequenza append-only (es. `BookCataloged`, `LoanRegistered`).
- **State Evolution:** lo stato corrente di ogni aggregato è derivato come una funzione pura degli eventi storici:
  
  $$\text{Stato}_{t} = \text{evolve}(\text{Stato}_{t-1}, \text{Evento}_t)$$

### Vantaggi Operativi nel Contesto Bibliotecario
- **Audit Trail Integrato:** cronistoria completa e inalterabile per ogni esemplare, prestito o scheda bibliografica.
- **Time Travel & Riproducibilità:** potenziale capacità di ricostruire con precisione matematica lo stato esatto del catalogo a qualsiasi data o versione temporale passata.
- **Resilienza ai Bug:** possibilità di correggere proiezioni di lettura e re-indicizzare il catalogo riproducendo semplicemente il flusso degli eventi (*event replay*).

---

## 2. Il Motore di Dominio: Sharpino

L'infrastruttura di gestione degli eventi e degli aggregati di BiblioNet.eu si basa su **Sharpino**, una libreria open source ideata per implementare Event Sourcing idiomatico su ecosistema .NET.

### Caratteristiche Chiave di Sharpino
- **Nativo F#:** massimizza l'uso del type system avanzato (Discriminated Unions, Record immutabili, pattern matching esaustivo) eliminando intere classi di errori a runtime.
- **PostgreSQL come Event Store:** persistenza append-only affidabile basata su standard relazionali ACID collaudati, senza la complessità operativa di cluster dedicati.
- **Snapshotting Automatico:** ottimizzazione delle performance per aggregati con stream di eventi prolungati, consentendo caricamenti dello stato istantanei.
- **Concorrenza Ottimistica e Transazionalità:** isolamento rigoroso per prevenire conflitti durante scritture concorrenti.

> **Risorse Open Source di Sharpino:**  
> - Repository GitHub: [github.com/tonyx/Sharpino](https://github.com/tonyx/Sharpino)  
> - Pacchetto NuGet: `Sharpino`

---

## 3. Stack Tecnologico

L'architettura software di BiblioNet.eu combina un backend funzionale rigoroso con interfacce web reattive e strumenti avanzati di ricerca:

| Livello / Componente | Tecnologia | Ruolo Architetturale |
| :--- | :--- | :--- |
| **Domain Logic** | F# (.NET) | Definizione dei tipi di dominio, contratti dei comandi/eventi ed esecuzione pura delle regole di business |
| **Event Persistence** | PostgreSQL | Event Store append-only e persistenza sicura degli snapshot |
| **Ricerca Vettoriale & AI** | PostgreSQL (`pgvector`) | Indicizzazione semantica dei testi e recupero concettuale per somiglianza vettoriale tra titoli e abstract |
| **Frontend & UI** | Blazor / C# | Interfaccia utente web ricca, modulare e integrata nell'ecosistema .NET |
| **Ispezione & Strumenti** | SharpLens (WASM / Bolero) | Cruscotto analitico per l'ispezione visiva degli stream di eventi e debug dello stato |

---

## 4. Filosofia Open Source & Trasparenza

BiblioNet crede che il software per la cultura e la conoscenza debba riflettere i medesimi valori di apertura dei beni che cataloga.

- **Codice Ispezionabile:** le librerie e i moduli centrali del framework applicativo sono distribuiti con licenze permissive (MIT / Apache 2.0).
- **Zero Lock-In:** i dati memorizzati rimangono in formati aperti e standardizzati; l'Event Store su database PostgreSQL standard garantisce che i dati possano essere sempre esportati, verificati o migrati.
- **Approccio Deterministico:** grazie all'assenza di effetti collaterali nello strato di dominio, il comportamento dell'applicazione è completamente testabile mediante unit test puramente algebrici e property-based testing.

---

## 5. Risorse e Riferimenti

- **Piattaforma:** [www.bibionet.eu](https://www.bibionet.eu)
- **Framework Event Sourcing:** [Sharpino su GitHub](https://github.com/tonyx/Sharpino)
* **Biblionet Source code:** [Biblionet - blazorBookLibrary - on GitHub](https://github.com/tonyx/blazorBookLibrary)
* **Technical Contact:** For research partnerships, inter-library interoperability, or security disclosures: `info@bibionet.eu`