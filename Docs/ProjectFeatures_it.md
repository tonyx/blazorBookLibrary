# Caratteristiche del Progetto e Highlights Architetturali: Blazor Book Library

Questo documento fornisce una panoramica completa delle funzionalità e della struttura interna di **Blazor Book Library**. È progettato per servire come riferimento per presentazioni e riassunti tecnici.

---

## 1. Panoramica del Progetto
Blazor Book Library è un sistema di archiviazione e prestito all'avanguardia, costruito con una filosofia "Privacy by Design" e "AI-First". Combina l'affidabilità dell'**Event Sourcing** con le capacità innovative dell'**IA Generativa** e della **Ricerca Semantica**.

---

## 2. Ruoli Utente e Funzionalità

### 👤 L'Utente Finale (Patrono)
*Focalizzato sulla scoperta e sul prestito semplificato.*

*   **Ricerca Catalogo Avanzata**: filtraggio tradizionale per Titolo, ISBN, Anno e Categoria.
*   **Scoperta Semantica tramite IA e Ricerca su Articoli/Paper**: Ricerca per significato anziché per parole chiave. Gli utenti possono descrivere ciò che cercano (es. *"una tragica storia d'amore ambientata durante la rivoluzione industriale"* oppure *"l'entanglement quantistico nei processi biologici"*) e il sistema trova i libri concettualmente rilevanti e i singoli paper/articoli inclusi nei volumi.
*   **Bibliotecario IA Conversazionale (Chatbot basato su RAG)**: Un assistente letterario e di ricerca interattivo che combina Retrieval-Augmented Generation (RAG) e modelli Gemini. Intrattiene dialoghi multi-turno con i lettori, risponde a domande complesse basandosi sul catalogo effettivo della biblioteca, fornisce link diretti ai libri e citazioni specifiche dei paper, con supporto vocale e una modalità opzionale di conoscenza letteraria generale.
*   **Scoperta della Biblioteca e Filtraggio per Prossimità**: I lettori possono impostare la propria posizione (tramite GPS del browser, mappa interattiva o coordinate manuali) per filtrare e scoprire le biblioteche pubbliche e i circuiti di condivisione ordinati per vicinanza geografica.
*   **Filtraggio basato su Tag**: Scoperta flessibile utilizzando tag d'archivio (es. "Prima Edizione", "Interesse Locale") con supporto per il filtraggio logico "OR".
*   **Disponibilità in Tempo Reale e Politiche di Circolazione**: Feedback istantaneo se un libro è "Disponibile", "Circolante (Solo Registrati)", "Circolante (Inclusi Ospiti)", "Sola Consultazione" o "Attualmente in Prestito".
*   **Prestiti e Prenotazioni per Membri Registrati**: I membri possono prendere in prestito i titoli disponibili o prenotare libri attualmente in prestito, ricevendo notifiche automatiche quando tornano disponibili.
*   **Prenotazioni per Ospiti Senza Barriere**: I visitatori occasionali delle biblioteche pubbliche possono prenotare direttamente i volumi aperti agli ospiti senza obbligo di registrazione, ritirandoli mediante PIN sicuro o verifica al banco del bibliotecario.
*   **Dashboard Personale**: Una vista unificata dei prestiti correnti, delle prenotazioni in sospeso e della cronologia di lettura personale.

### 📚 Il Manager (Bibliotecario)
*Focalizzato sull'eccellenza del catalogo e sull'integrità dei dati.*

*   **Libri Multi-Articolo e Indicizzazione ("Papers")**:
    *   **Catalogazione di Contributi Singoli**: Registrazione di singoli paper, saggi e articoli all'interno di volumi collettivi (antologie, atti di convegni, riviste scientifiche) con autori e titoli dedicati.
    *   **Scansione OCR dell'Indice tramite Fotocamera**: Scatto fotografico del sommario o dell'indice del libro stampato; la computer vision dell'IA estrae titoli e autori degli articoli per un'aggiunta immediata con un click.
    *   **Embedding di Abstract e Testi**: Generazione di vettori ad alta dimensionalità per gli abstract o i testi trascritti dei paper in PostgreSQL pgvector, rendendo i singoli articoli direttamente rintracciabili dalla ricerca semantica e dal Bibliotecario IA.
    *   **Sincronizzazione degli Embedding Fratelli**: Collegamento e sincronizzazione degli embedding dei paper fratelli all'interno dello stesso libro per valorizzare le correlazioni semantiche interne.
*   **Gestione della Geolocalizzazione della Biblioteca**:
    *   **Mappatura Interattiva delle Coordinate**: Configurazione e rimozione facoltativa delle coordinate geografiche (latitudine e longitudine) della biblioteca tramite mappa interattiva Leaflet, GPS o immissione manuale.
    *   **Visibilità per la Comunità Locale**: I circuiti pubblici provvisti di coordinate possono essere individuati dai lettori vicini tramite il filtro per distanza.
*   **Catalogazione Assistita dall'IA**:
    *   **Integrazione Google Books**: Autocompletamento dei metadati da un singolo ISBN o Titolo.
    *   **Sintesi delle Descrizioni tramite IA**: Generazione automatica di riassunti di alta qualità per libri rari o antichi privi di record digitali.
    *   **Riconoscimento Copertina tramite IA**: Registrazione dei libri scattando una foto della copertina; l'IA risolve titolo, autori e metadati.
    *   **Undo Narrativo**: Un meccanismo di sicurezza per ripristinare i contenuti generati dall'IA se non soddisfano gli standard d'archivio.
*   **Sistema di Tagging d'Archivio**: Categorizzazione dei libri utilizzando un sistema di tag multi-tipo (Libro, Autore, Persona, Generale) con indicatori visivi colorati.
*   **Registro Autori**: Gestione di un database di creatori con **Scoperta del Ritratto via Wikipedia** automatizzata e recupero dei link biografici.
*   **Interoperabilità Bibliografica e Conformità agli Standard**:
    *   **Esportazione Archivistica Universale**: Esportazione completa di singoli volumi o di interi cataloghi in 8 standard internazionali bibliotecari ed editoriali: **Dublin Core (DCMES 1.1)**, **MARC21 / MARCXML**, **MODS 3.0**, **ONIX for Books 3.0**, **BibTeX**, **Schema.org (JSON-LD)**, **JSON** e **CSV**.
    *   **Azione Pubblica "Cita ed Esporta"**: Copia della citazione negli appunti con un solo clic e download diretto del file nelle pagine di dettaglio libro.
    *   **REST API Aperta per il Catalogo**: API HTTP headless multi-tenant (`/api/catalog`) con ricerca per ISBN, SBN (ICCU) e GUID aggregato, negoziazione del formato (`?format=` e header `Accept`) e autorizzazione via API key.
*   **Operazioni Massive**:
    *   **Esportazione Catalogo Multi-Formato**: Download dell'intero archivio del tenant in ciascuno degli 8 formati bibliografici supportati.
    *   **Importazione di Massa via ISBN**: Caricamento di elenchi di ISBN per la registrazione batch automatizzata.
    *   **Modifica Massiva dei Metadati**: Aggiornamento simultaneo di categorie, anni o stato per centinaia di record.
*   **Sigillatura dei Record**: Protezione dei record d'archivio verificati da modifiche accidentali tramite la "Sigillatura".

### ⚙️ L'Amministratore
*Focalizzato sulla salute del sistema, la sicurezza e la conformità.*

*   **Gestione Utenti e Ruoli**: Controllo granulare sugli accessi alla biblioteca, elevando gli utenti ai ruoli di Manager o Bibliotecario.
*   **Conformità GDPR ("Diritto all'Oblio")**: 
    *   **Pattern di Ghosting**: Anonimizzazione dei dati PII degli utenti (Email, Nome, Codice Fiscale) preservando l'integrità archivistica della cronologia dei prestiti.
*   **Pannello di Manutenzione del Sistema**:
    *   **Riconciliazione DB Vettoriale**: Strumenti per sincronizzare il database di ricerca semantica con l'event store, eliminando gli embedding orfani.
    *   **Controlli di Integrità degli Embedding**: Rilevamento e correzione di libri con dati semantici mancanti o obsoleti.
*   **Sicurezza Avanzata**:
    *   **Protezione dai Bot**: Integrazione di reCAPTCHA e analisi del bot-score per prevenire lo scraping automatizzato.
    *   **Audit Trail**: Ogni modifica nel sistema è un evento immutabile, fornendo una cronologia perfetta di tutte le azioni sul catalogo e sugli utenti.

---

## 3. Eccellenza Tecnica (Struttura Interna)

### 🏗️ Architettura: Lo Stack "Sanctuary"
*   **Core Funzionale in F#**: La logica di dominio è scritta in F#, sfruttando le Discriminated Union e i tipi Result per rendere irrappresentabili gli stati non validi.
*   **Event Sourcing (Sharpino)**: Invece di memorizzare solo lo "stato corrente", il sistema memorizza l'intera cronologia degli eventi. Ciò consente:
    *   **Audit Perfetto**: Sapere esattamente chi ha cambiato cosa e quando.
    *   **Viaggio nel Tempo**: La capacità di ricostruire lo stato della biblioteca in qualsiasi momento della storia.
    *   **Scalabilità**: Modelli di lettura (Details) ottimizzati e disaccoppiati dal lato scrittura.
*   **UI Blazor InteractiveServer**: Un'interfaccia C# ricca e reattiva che fornisce un'esperienza "simile a un desktop" nel browser con aggiornamenti in tempo reale.

### 🤖 Infrastruttura IA e Ricerca
*   **Database Vettoriale Semantico Multi-Livello**: Alimentato da **PostgreSQL + pgvector**, memorizza embedding ad alta dimensionalità partizionati per tipologia di entità (descrizioni di `book` e abstract/testi di `paper`).
*   **Integrazione Large Language Model**: Utilizza **Gemini 3.6 Flash** (e Vision) per la risoluzione dei metadati, la sintesi del testo e le spiegazioni dei match.
*   **Motore Conversazionale RAG (Bibliotecario IA)**: Orchestra la ricerca di similarità vettoriale con la memoria della conversazione, iniettando il contesto mirato dei libri e dei paper nei prompt di Gemini per produrre risposte documentate e arricchite da citazioni.
*   **Motore di Prossimità Geografica**: Utilizza il calcolo della distanza sferica (formula di Haversine) nei servizi di dominio F# e l'interoperabilità JS con mappe interattive Leaflet per filtrare e ordinare i circuiti pubblici in base alla posizione dell'utente.
*   **Spiegazioni dei Match**: L'IA non trova solo un libro; può spiegare *perché* corrisponde a una query semantica.

### 📦 Infrastruttura e Affidabilità
*   **PostgreSQL**: Storage relazionale solido per eventi e snapshot.
*   **Message Bus e Azure Sql Db (Optionali)**: Supporto per il caching distribuito ad alte prestazioni.
*   **Mailer in Background Affidabile**: Un worker dedicato che riprova le notifiche fallite (es. promemoria di prestito) per garantire la consegna.

---

## 4. Tabella di Marcia Futura
*   **Browser di Eventi**: Uno strumento amministrativo di basso livello per visualizzare il flusso live degli eventi d'archivio.
*   **Pianificazione Automatica**: Servizi in background per la pulizia automatizzata del catalogo e la sincronizzazione dei metadati.
*   **Archiviazione Sociale**: Tagging collaborativo e sistemi di recensioni guidati dalla comunità.
