# Manuale Utente: Blazor Book Library

Benvenuti nel manuale utente di **Blazor Book Library**. Questa guida ti aiuterà a navigare nel sistema, comprendere il concetto di multi-tenancy, gestire i cataloghi ed esplorare le funzionalità avanzate della piattaforma.

---

## Tabella dei Contenuti
1. [Introduzione](#introduzione)
2. [Guida all'Avvio e Multi-Tenancy](#guida-allavvio-e-multi-tenancy)
    - [L'Architettura Multi-Tenant (Circuiti)](#larchitettura-multi-tenant-circuiti)
    - [Login e Autenticazione](#login-e-autenticazione)
    - [Contesto Attivo e Cambio di Circuito](#contesto-attivo-e-cambio-di-circuito)
3. [Funzionalità Principali per Tutti gli Utenti](#funzionalità-principali-per-tutti-gli-utenti)
    - [Ricerca e Scoperta](#ricerca-e-scoperta)
    - [Bibliotecario IA (Assistente Virtuale Basato su RAG)](#bibliotecario-ia)
    - [Visualizzazione Dettagli Libro e Articoli](#visualizzazione-dettagli-libro)
4. [Privilegi dei Patron e Circolazione](#privilegi-dei-patron-e-circolazione)
    - [Accettare un Invito a un Circuito](#accettare-un-invito-a-un-circuito)
    - [Prestiti e Restituzioni (Regole di Circolazione)](#prestiti-e-restituzioni-regole-di-circolazione)
    - [Prenotazioni](#prenotazioni)
5. [Strumenti per Proprietari di Tenant e Manager](#strumenti-per-proprietari-di-tenant-e-manager)
    - [Creare il Proprio Tenant (Library-as-a-Service)](#creare-il-proprio-tenant-library-as-a-service)
    - [Visibilità e Impostazioni del Tenant (Pubblico vs. Privato)](#visibilità-e-impostazioni-del-tenant-pubblico-vs-privato)
    - [Elenco Patron e Delega dei Ruoli](#elenco-patron-e-delega-dei-ruoli)
    - [Gestione dei Punti di Distribuzione e Custodia](#gestione-dei-punti-di-distribuzione-e-custodia)
    - [Gestione del Catalogo](#gestione-del-catalogo)
    - [Libri con Più Articoli e Indicizzazione ("Papers")](#libri-con-piu-articoli)
    - [Arricchimento tramite IA ed Embedding Vettoriali](#arricchimento-tramite-ia-ed-embedding)
    - [Importazione ed Esportazione Archivio](#importazione-ed-esportazione-archivio)
6. [Amministrazione del Sistema](#amministrazione-del-sistema)
    - [Gestione Utenti](#gestione-utenti)
    - [Pannello di Controllo Admin e Riconciliazione](#pannello-di-controllo-admin-e-riconciliazione)
    - [Anonimizzazione GDPR e "Diritto all'Oblio"](#anonimizzazione-gdpr-e-diritto-alloblio)
    - [Affidabilità del Servizio Email](#affidabilità-del-servizio-email)
7. [Risoluzione dei Problemi e Supporto](#risoluzione-dei-problemi-e-supporto)

---

<a name="introduzione"></a>
## 1. Introduzione
Blazor Book Library è un sistema di archiviazione e prestito moderno e ad alte prestazioni. È alimentato da un'architettura basata su event-sourcing e sfrutta integrazioni IA avanzate (come la ricerca semantica e il riconoscimento delle copertine) per rendere la catalogazione e la scoperta dei libri un'esperienza immediata.

Invece di utilizzare un unico database condiviso in cui tutti gli utenti visualizzano gli stessi dati, la piattaforma organizza libri, prestiti e membri in ecosistemi di prestito isolati chiamati **Circuiti** (o **Tenant**). Questa guida ti illustrerà sia l'esperienza di utilizzo standard della biblioteca sia il processo per gestire il tuo servizio bibliotecario digitale indipendente.

---

<a name="guida-allavvio-e-multi-tenancy"></a>
## 2. Guida all'Avvio e Multi-Tenancy

<a name="larchitettura-multi-tenant-circuiti"></a>
### L'Architettura Multi-Tenant (Circuiti)
Per servire diverse comunità e collezionisti di libri, il sistema funziona secondo un modello multi-tenant auto-governato:
1. **Il Circuito di Dimostrazione (Tenant di Default)**: Al momento della registrazione, tutti gli utenti si uniscono ed esplorano automaticamente il **Tenant di Default** (`TenantId.Default`). Questo funge da sandbox pubblica comune contenente un catalogo predefinito, categorie di esempio e punti di distribuzione dimostrativi, consentendo un'esplorazione immediata.
2. **Circuiti Autonomi (Library-as-a-Service)**: Qualsiasi utente registrato può creare istantaneamente un **Tenant** completamente isolato in qualsiasi momento. In qualità di **Proprietario (Owner)**, avrai il controllo assoluto su questo circuito personalizzato: potrai catalogare i tuoi libri, invitare amici o vicini, assegnare ruoli e decidere le politiche di accesso senza interferire con nessun altro utente.

<a name="login-and-authentication"></a>
### Login e Autenticazione
- Accedi al sistema tramite il link **Login** nel menu di navigazione.
- Puoi registrare un nuovo account o accedere con le tue credenziali esistenti.
- Il sistema supporta il **Social Login** (es. Google OAuth), l'**Autenticazione Passkey** e l'**Autenticazione a Due Fattori (2FA)** per un accesso sicuro e moderno.

<a name="contesto-attivo-e-cambio-di-circuito"></a>
### Contesto Attivo e Cambio di Circuito
- Il circuito attivo determina ciò che vedi. Ogni consultazione del catalogo, ricerca semantica, verifica dei punti di distribuzione o transazione di prestito opera rigorosamente entro i confini del tuo **Tenant Attivo**.
- Cambia contesto facilmente utilizzando il menu a discesa **Selettore Tenant** situato nella barra di navigazione. Seleziona qualsiasi circuito di tua proprietà o a cui appartieni come patron per aggiornare istantaneamente il contesto della tua area di lavoro.

---

<a name="funzionalità-principali-per-tutti-gli-utenti"></a>
## 3. Funzionalità Principali per Tutti gli Utenti

<a name="ricerca-e-scoperta"></a>
### Ricerca e Scoperta
La pagina **Ricerca Biblioteca** è progettata per interrogare il catalogo del tenant attivo:
- **Ricerca per Titolo**: Inserisci qualsiasi parte del titolo di un libro.
- **Ricerca per ISBN e Codice SBN**: Trova un libro specifico tramite il suo codice ISBN a 10 o 13 cifre oppure tramite il codice del Servizio Bibliotecario Nazionale (S.B.N.).
- **Filtri Avanzati**: Filtra per uno o più autori, generi/categorie, intervalli di pubblicazione o mostra solo i libri immediatamente disponibili.
- **Scoperta Semantica IA**:
    - Inserisci query in linguaggio naturale (es. "una storia distopica sulla perdita di memoria e il controllo totalitario") per recuperare libri con argomenti semanticamente correlati, anche se non condividono le parole chiave esatte.
    - Le ricerche valutano sia le descrizioni complessive dei volumi sia i singoli articoli o paper inclusi nei volumi collettivi.
    - Limita il numero di risultati restituiti per affinare le liste di scoperta.

<a name="bibliotecario-ia"></a>
### Bibliotecario IA (Assistente Virtuale Basato su RAG)
Accessibile dal menu di navigazione principale (**Bibliotecario IA**), questo assistente conversazionale funge da consulente letterario e di ricerca digitale:
- **Architettura RAG (Retrieval-Augmented Generation)**: L'assistente sfrutta embedding vettoriali avanzati per ricercare e recuperare i libri e i singoli paper/articoli rilevanti dalla collezione del tuo tenant attivo, fornendo questo contesto mirato direttamente al modello **Google Gemini (Gemini 3.5 Flash)**.
- **Interviste Interattive sulla Conoscenza**: I lettori possono intervistare il Bibliotecario IA su qualsiasi argomento, concetto, tesi o evento storico presente nel catalogo della biblioteca.
- **Risposte Radicate e Citazioni Puntuali**: Quando risponde a partire dal catalogo, il bibliotecario fornisce link cliccabili diretti ai libri (`[Titolo](book://<book-id>)`) e cita esplicitamente i singoli paper o articoli pertinenti, evidenziando le corrispondenze dirette e attribuendoli ai rispettivi autori.
- **Modalità Conoscenza Letteraria Generale ed Extra-Catalogo**: Per impostazione predefinita, il bibliotecario ancora rigorosamente le proprie risposte al catalogo effettivo della biblioteca. Attivando l'opzione **"Includi Conoscenza Generale"** (o richiedendo informazioni esterne), l'assistente attinge alla vasta conoscenza incorporata o esplorabile dal modello Gemini 3.5 Flash stesso, suggerendo opere esterne o approfondendo temi storici/filosofici più ampi, contrassegnando chiaramente le opere non attualmente presenti nella biblioteca.

<a name="visualizzazione-dettagli-libro"></a>
### Visualizzazione Dettagli Libro e Articoli
Cliccando su un libro si apre la pagina **Visualizza Libro**:
- **Panoramica**: Visualizza immagini di copertina, riassunti e categorie.
- **Articoli e Paper Raccolti**: Per volumi contenenti più contributi (antologie, atti di convegni, riviste), esplora l'elenco completo dei singoli paper con i rispettivi autori e abstract.
- **Posizione e Custodia**: Verifica quale **Punto di Distribuzione** fisico contiene attualmente il libro.
- **Stato Disponibilità**: Vedi a colpo d'occhio se il libro è sullo scaffale, in prestito (con data di restituzione prevista) o contrassegnato solo per consultazione.

---

<a name="privilegi-dei-patron-e-circolazione"></a>
## 4. Privilegi dei Patron e Circolazione

<a name="accettare-un-invito-a-un-circuito"></a>
### Accettare un Invito a un Circuito
Se un amico, un familiare o un leader di una comunità ti invita nel suo tenant privato, riceverai un'email di invito contenente un link sicuro.
1. Clicca sul link dinamico presente nell'email.
2. Accedi o crea un nuovo account se non lo hai già fatto.
3. Il sistema converte l'invito registrandoti nel ruolo di **Patron** all'interno del tenant ospitante, imposta il tuo contesto tenant attivo e salva un cookie sicuro (`selected_tenant`).
4. Verrai reindirizzato alla nuova dashboard personalizzata per iniziare subito a sfogliare la collezione.

<a name="prestiti-e-restituzioni-regole-di-circolazione"></a>
### Prestiti e Restituzioni (Regole di Circolazione)
- **Prestito**: Vai alla pagina dei dettagli di un libro disponibile e clicca su **"Prendi in prestito"**. Puoi selezionare un **Punto di Distribuzione** fisico per coordinare il ritiro.
- **Restituzioni (Vincolo Stretto)**: Per preservare la tracciabilità dell'inventario fisico, **i libri devono essere restituiti all'esatto Punto di Distribuzione fisico** da cui sono stati presi in prestito o registrati.
- **Self-Service vs. Approvazione del Custode**:
  - In un tenant **Gestito da Custodi**, un referente designato (Utente di Riferimento) deve controllare fisicamente e approvare il ritiro o la restituzione nel sistema.
  - In un tenant **Self-Service (Basato sulla Fiducia)**, i patron confermano direttamente i loro ritiri e restituzioni digitali, affidandosi interamente alla fiducia reciproca e all'onestà.

<a name="prenotazioni"></a>
### Prenotazioni
Se un libro è attualmente in prestito, clicca su **"Prenota"** per metterti in coda. Verrai avvisato via email al momento della restituzione. Le prenotazioni possono essere annullate in qualsiasi momento dalla dashboard del tuo profilo.

---

<a name="strumenti-per-proprietari-di-tenant-e-manager"></a>
## 5. Strumenti per Proprietari di Tenant e Manager

<a name="creare-il-proprio-tenant-library-as-a-service"></a>
### Creare il Proprio Tenant (Library-as-a-Service)
Per ospitare il tuo circuito di condivisione:
1. Naviga nella **Dashboard Tenant** e seleziona **"Crea Nuovo Circuito"**.
2. Assegna un nome univoco al tuo circuito.
3. Al momento della creazione, verrai registrato come **Proprietario (Owner)** del nuovo contesto tenant, ottenendo il controllo amministrativo assoluto.

<a name="visibilità-e-impostazioni-del-tenant-pubblico-vs-privato"></a>
### Visibilità e Impostazioni del Tenant (Pubblico vs. Privato)
I proprietari possono attivare e disattivare la visibilità del proprio tenant:
- **🔒 Privato (Cerchio Chiuso)**: Praticamente invisibile. Il catalogo e i dettagli sono completamente nascosti agli utenti generici della piattaforma. Gli utenti devono essere invitati via email per sfogliare o prendere in prestito. Perfetto per scaffali familiari, club del libro o ristrette cerchie di amici.
- **🏛️ Pubblico (Biblioteca Comunitaria)**: Ricercabile e consultabile da qualsiasi utente registrato sulla piattaforma. Ideale per spazi di coworking, caffè di quartiere, micro-biblioteche pubbliche e reti di bookcrossing.

<a name="elenco-patron-e-delega-dei-ruoli"></a>
### Elenco Patron e Delega dei Ruoli
In qualità di Proprietario del Tenant:
- **Onboarding dei Patron**: Inserisci l'email di un utente nella sezione **"Invita Patron"** per inviare un invito via email. È possibile visualizzare tutti gli inviti in sospeso e revocarli se necessario.
- **Assegnazione Manager**: Promuovi i Patron attivi al ruolo di **Manager**, consentendo loro di gestire libri, autori, categorie e generare embedding IA.
- **Rimozione / Revoca**: Retrocedi i manager a patron o rimuovi completamente i membri inattivi dall'elenco del tuo tenant.

<a name="gestione-dei-punti-di-distribuzione-e-custodia"></a>
### Gestione dei Punti di Distribuzione e Custodia
Definisci l'infrastruttura fisica in cui risiedono i libri (es. "Scaffale Soggiorno", "Armadietto Ufficio", "Box Caffè Verde"):
1. Crea nuovi **Punti di Distribuzione** all'interno del tuo tenant.
2. Scegli un **Modello di Prestito**:
   - **Self-Service**: Disattiva la verifica del custode. Gli utenti prendono in prestito e restituiscono i libri in autonomia.
   - **Custodia Delegata**: Assegna uno o più patron come **Utenti di Riferimento** per posizioni specifiche. Queste persone dovranno approvare fisicamente le azioni nel sistema.

<a name="gestione-del-catalogo"></a>
### Gestione del Catalogo
I manager e i proprietari utilizzano il registro **Gestione Libri**:
- **Aggiungi Nuovo Libro**: Inserisci i dettagli manualmente o scansiona il codice a barre di un libro fisico utilizzando la fotocamera del tuo dispositivo per recuperare i metadati da Google Books.
- **Riconoscimento Copertina IA**: Se un codice a barre è danneggiato o assente, clicca sull'icona della fotocamera, scatta una foto nitida della copertina e lascia che la nostra visione IA identifichi titolo, autori e metadati.
- **Registro Autori**: Gestisci i creatori, ricerca le biografie e importa i ritratti direttamente da Wikipedia. Puoi **"Sigillare"** i profili degli autori per bloccarli contro modifiche accidentali.
- **Codici S.B.N. Italiani e Ambito di Interconnessione**: Il sistema consente di associare uno specifico identificativo nel formato ampiamente utilizzato dal **S.B.N. (Servizio Bibliotecario Nazionale)** italiano (es. *CFI0001234*) per fini di riferimento incrociato, coerenza catalografica e deduplica. Tuttavia, il sistema non fornisce un'interconnessione diretta o sincronizzazione in tempo reale con l'infrastruttura legacy dell'S.B.N., poiché i due sistemi perseguono finalità differenti: BiblioNet (*The Modern Archivist*) è un modello agile, snello e orientato all'intelligenza artificiale, concepito per circuiti di comunità indipendenti e per la ricerca semantica, mentre l'S.B.N. legacy funge da dorsale centralizzata per l'intero sistema bibliotecario nazionale italiano. Pertanto, ad eccezione dell'implementazione e validazione del formato del codice S.B.N. all'interno del catalogo per ricerca e cross-referencing, le interconnessioni dirette con il sistema legacy sono attualmente fuori dall'ambito della piattaforma.

<a name="libri-con-piu-articoli"></a>
### Libri con Più Articoli e Indicizzazione ("Papers")
Molti volumi del catalogo—come atti di convegno, raccolte di saggi, antologie o riviste collettanee—racchiudono molteplici articoli o paper scritti da autori differenti. Il sistema offre strumenti dedicati per catalogare, scansionare e indicizzare ciascun contributo all'interno di qualsiasi libro:
- **Scansione con Fotocamera dell'Indice / Sommario**: Invece di inserire manualmente ciascun articolo, i manager possono attivare la scansione con fotocamera dall'editor del libro per fotografare l'indice cartaceo o il sommario. Il modello di visione IA analizza automaticamente la pagina, individua i titoli degli articoli e i rispettivi autori, e li predispone per l'aggiunta con un solo clic.
- **Inserimento Testuale e Manuale**: È anche possibile incollare il testo del sommario copiato o aggiungere manualmente i singoli paper specificando titolo e associandoli ad autori esistenti o nuovi.
- **Embedding Vettoriali per Singolo Paper**: Per ogni articolo, i manager possono cliccare su **"Incorpora Contenuto"** (o "Aggiorna Embedding") per fornire un abstract riassuntivo o l'intero testo dell'articolo. È possibile digitare/incollare il testo o utilizzare la **Scansione OCR Multi-Pagina con Fotocamera** per fotografare e trascrivere in sequenza le pagine dell'articolo fisico.
- **Raggiungibilità Semantica**: La generazione dell'embedding del paper memorizza un vettore dedicato nel database vettoriale (`item_type = "paper"`). Questo garantisce che il contenuto dettagliato, le tesi e la terminologia specifica di ogni singolo paper siano direttamente accessibili e interrogabili tramite ricerche semantiche e attraverso il sistema RAG del Bibliotecario IA.
- **Collegamento degli Embedding Fratelli (Sibling)**: Con un solo clic, i manager possono sincronizzare e collegare reciprocamente tutti gli embedding dei paper appartenenti allo stesso volume, ottimizzando le correlazioni semantiche intra-libro.

<a name="arricchimento-tramite-ia-ed-embedding"></a>
### Arricchimento tramite IA ed Embedding Vettoriali
- **Genera Descrizioni**: Clicca su **"Genera Descrizione"** per fare scrivere all'IA un riassunto completo basato sul titolo e sui metadati. È possibile annullare (Undo) la generazione se necessario.
- **Embedding Vettoriali di Volume e di Paper**: Per abilitare la ricerca semantica IA per l'intero volume, clicca su **"Genera Embedding"** nella pagina di modifica del libro. L'IA converte le descrizioni in vettori matematici. Analogamente, i singoli paper possono essere incorporati semanticamente con abstract o testo integrale.
- **Controllo di Integrità**: Verifica la precisione dei tuoi indici semantici digitando query in linguaggio naturale direttamente nel pannello di validazione per vedere dove si posiziona il libro.

<a name="importazione-ed-esportazione-archivio"></a>
### Importazione ed Esportazione Archivio
- **Esportazione**: Scarica istantaneamente il catalogo del tenant attivo in formato **CSV** o **JSON** per backup digitali o stampe cartacee.
- **Importazione ISBN Massiva**: Incolla una lista di ISBN (uno per riga) o carica un file di testo. Personalizza le opzioni per recuperare automaticamente i metadati, generare descrizioni mancanti, creare profili autore e creare embedding semantici al volo con un monitor di avanzamento in tempo reale.

---

<a name="amministrazione-del-sistema"></a>
## 6. Amministrazione del Sistema

<a name="gestione-utenti"></a>
### Gestione Utenti
Gli Amministratori Globali supervisionano la sicurezza della piattaforma e i ruoli a livello di sistema attraverso il **Gestore Utenti**, cercando profili, esaminando lo stato globale e assegnando ruoli di manager globali.

<a name="pannello-di-controllo-admin-e-riconciliazione"></a>
### Pannello di Controllo Admin e Riconciliazione
Fornisce controlli di integrità a livello di piattaforma:
- **Pulisci Vettori Orfani**: Rimuove gli embedding di ricerca obsoleti non legati a libri esistenti.
- **Sincronizzazione Stati Libri**: Corregge la discrepanza in cui i libri fanno riferimento a puntatori di database vettoriali non validi.

<a name="anonimizzazione-gdpr-e-diritto-alloblio"></a>
### Anonimizzazione GDPR e "Diritto all'Oblio"
I membri possono attivare l'anonimizzazione nella sezione **"Gestisci i tuoi dati"**. Per mantenere l'integrità del catalogo e dello storico dei prestiti nel rispetto delle normative sulla privacy:
- Le informazioni personali (Email, Nome, Codice Fiscale) vengono completamente sostituite con dati casuali.
- L'account utente viene disattivato in modo permanente.
- Le transazioni storiche di prestito sono conservate come record "fantasma" anonimi.

<a name="affidabilità-del-servizio-email"></a>
### Affidabilità del Servizio Email
Un worker in background elabora le consegne delle notifiche in coda (es. inviti, conferme), riprovando l'invio delle email fallite ogni 10 minuti per garantire la massima affidabilità.

---

<a name="risoluzione-dei-problemi-e-supporto"></a>
## 7. Risoluzione dei Problemi e Supporto

| Problema | Possibile Soluzione |
| :--- | :--- |
| **Email non ricevuta** | Controlla la cartella Spam. Se assente, il worker in background riproverà l'invio entro 10 minuti. |
| **Impossibile vedere il catalogo invitato** | Assicurati di aver accettato il link di invito e che la tua area di lavoro attiva sia impostata sul nuovo tenant. |
| **Scansione o acquisizione copertina fallita** | Assicurati che ci sia una buona illuminazione e un posizionamento piatto del volume. Se continua a fallire, inserisci manualmente l'ISBN per l'autofill. |
| **Il record è bloccato** | Controlla se il libro o l'autore è "Sigillato". Un Proprietario, Manager o Admin deve sbloccarlo prima di poter applicare aggiornamenti. |
| **Restituzione di circolazione bloccata** | I libri devono essere restituiti allo specifico Punto di Distribuzione presso cui sono registrati. Seleziona la destinazione corretta. |

---
*Per i dettagli sull'architettura tecnica, consulta [Architecture.md](https://github.com/tonyx/blazorBookLibrary/blob/main/Docs/Architecture.md)
