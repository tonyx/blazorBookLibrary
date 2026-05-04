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
*   **Scoperta Semantica tramite IA**: Ricerca per significato piuttosto che per parole chiave. Gli utenti possono descrivere ciò che stanno cercando (es. *"una tragica storia d'amore ambientata durante la rivoluzione industriale"*) e il sistema trova i libri concettualmente rilevanti.
*   **Filtraggio basato su Tag**: Scoperta flessibile utilizzando tag d'archivio (es. "Prima Edizione", "Interesse Locale") con supporto per il filtraggio logico "OR".
*   **Disponibilità in Tempo Reale**: Feedback istantaneo se un libro è "Immediatamente Disponibile", "Sola Consultazione" o "Attualmente in Prestito".
*   **Prestiti e Prenotazioni Self-Service**: I membri possono prendere in prestito i titoli disponibili o prenotare libri attualmente in prestito, ricevendo notifiche automatiche quando tornano disponibili.
*   **Dashboard Personale**: Una vista unificata dei prestiti correnti, delle prenotazioni in sospeso e della cronologia di lettura personale.

### 📚 Il Manager (Bibliotecario)
*Focalizzato sull'eccellenza del catalogo e sull'integrità dei dati.*

*   **Catalogazione Assistita dall'IA**:
    *   **Integrazione Google Books**: Autocompletamento dei metadati da un singolo ISBN o Titolo.
    *   **Sintesi delle Descrizioni tramite IA**: Generazione automatica di riassunti di alta qualità per libri rari o antichi privi di record digitali.
    *   **Riconoscimento Copertina tramite IA**: Registrazione dei libri scattando una foto della copertina; l'IA risolve titolo, autori e metadati.
    *   **Undo Narrativo**: Un meccanismo di sicurezza per ripristinare i contenuti generati dall'IA se non soddisfano gli standard d'archivio.
*   **Sistema di Tagging d'Archivio**: Categorizzazione dei libri utilizzando un sistema di tag multi-tipo (Libro, Autore, Persona, Generale) con indicatori visivi colorati.
*   **Registro Autori**: Gestione di un database di creatori con **Scoperta del Ritratto via Wikipedia** automatizzata e recupero dei link biografici.
*   **Operazioni Massive**:
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
*   **Database Vettoriale Semantico**: Alimentato da **PostgreSQL + pgvector**, memorizza embedding ad alta dimensionalità delle descrizioni dei libri.
*   **Integrazione Large Language Model**: Utilizza **GPT-4** (e Vision) per la risoluzione dei metadati, la sintesi del testo e le spiegazioni dei match.
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
