# Manuale Utente: Blazor Book Library

Benvenuti nel manuale di **Blazor Book Library**. Questa guida ti aiuterà a navigare nel sistema, gestire il catalogo e comprendere le funzionalità principali della piattaforma.

---

## Tabella dei Contenuti
1. [Introduzione](#introduzione)
2. [Sia Inizia](#per-iniziare)
3. [Funzionalità per Tutti gli Utenti](#funzionalità-per-tutti-gli-utenti)
    - [Ricerca e Scoperta](#ricerca-e-scoperta)
    - [Visualizzazione Dettagli Libro](#visualizzazione-dettagli-libro)
4. [Privilegi dei Membri](#privilegi-dei-membri)
    - [Prestiti e Restituzioni](#prestiti-e-restituzioni)
    - [Prenotazioni](#prenotazioni)
5. [Strumenti per Bibliotecari e Manager](#strumenti-per-bibliotecari-e-manager)
    - [Gestione del Catalogo](#gestione-del-catalogo)
    - [Scansione Barcode](#scansione-barcode)
    - [Registro Autori](#registro-autori)
    - [Operazioni Massive](#operazioni-massive)
6. [Amministrazione del Sistema](#amministrazione-del-sistema)
    - [Gestione Utenti](#gestione-utenti)
    - [Affidabilità del Servizio Email](#affidabilità-del-servizio-email)
7. [Risoluzione dei Problemi e Supporto](#risoluzione-dei-problemi-e-supporto)

---

<a name="introduzione"></a>
## 1. Introduzione
Blazor Book Library è un sistema di archiviazione moderno e ad alte prestazioni, progettato per la scopribilità e la facilità d'uso. Sfrutta l'event-sourcing per una tracciabilità perfetta e si integra con servizi globali come Google Books per mantenere il catalogo ricco e accurato.

<a name="per-iniziare"></a>
## 2. Per Iniziare
### Login e Autenticazione
- Accedi al sistema tramite il link **Login** nel menu di navigazione.
- Puoi registrare un nuovo account o accedere con le tue credenziali esistenti.
- Il sistema supporta il **Social Login** (es. Google OAuth) per un'esperienza immediata.

### Dashboard Principale
- Una volta effettuato l'accesso, la pagina **Home** offre una panoramica rapida delle ultime aggiunte in biblioteca.
- Usa la barra laterale o la navigazione superiore per passare alla **Ricerca Biblioteca**, al **Gestore Libri** o al **Gestore Autori** (a seconda del tuo ruolo).

---

<a name="funzionalità-per-tutti-gli-utenti"></a>
## 3. Funzionalità per Tutti gli Utenti

<a name="ricerca-e-scoperta"></a>
### Ricerca e Scoperta
La pagina di **Ricerca Biblioteca** è lo strumento principale per trovare letteratura.
- **Ricerca per Titolo**: Inserisci qualsiasi parte del titolo di un libro.
- **Ricerca per ISBN**: Trova un libro specifico tramite il suo codice ISBN a 10 o 13 cifre.
- **Filtri Avanzati**:
    - **Autori**: Filtra per uno o più autori.
    - **Categorie**: Filtra per genere o classificazione (es. Fiction, Scienza, Storia).
    - **Cronologia**: Cerca libri pubblicati in un anno specifico o in un intervallo.
    - **Disponibilità**: Filtra per mostrare solo i libri immediatamente disponibili per il prestito.

<a name="visualizzazione-dettagli-libro"></a>
### Visualizzazione Dettagli Libro
Cliccando sul titolo di un libro si apre la pagina **Visualizza Libro**.
- **Panoramica**: Visualizza immagini di copertina, riassunti e metadati.
- **Stato Disponibilità**: Vedi a colpo d'occhio se il libro è sullo scaffale, in prestito o solo per consultazione.
- **Date di Restituzione**: Se in prestito, viene visualizzata la data di restituzione prevista.

---

<a name="privilegi-dei-membri"></a>
## 4. Privilegi dei Membri

<a name="prestiti-e-restituzioni"></a>
### Prestiti e Restituzioni
I membri possono gestire i propri prestiti.
- **Prestito**: Vai alla pagina dei dettagli del libro e clicca su **"Prendi in prestito"** se il libro è disponibile.
- **Restituzione**: I prestiti attivi possono essere conclusi cliccando su **"Restituisci"**, rendendo il libro nuovamente disponibile per gli altri.

<a name="prenotazioni"></a>
### Prenotazioni
Se un libro è attualmente in prestito, puoi effettuare una **Prenotazione**.
- Verrai avvisato quando il libro verrà restituito e sarà riservato per te.
- Le prenotazioni possono essere annullate in qualsiasi momento dal tuo profilo o dalla pagina del libro.

---

<a name="strumenti-per-bibliotecari-e-manager"></a>
## 5. Strumenti per Bibliotecari e Manager

<a name="gestione-del-catalogo"></a>
### Gestione del Catalogo
I manager utilizzano il **Gestore Libri** per mantenere l'eccellenza della biblioteca.
- **Aggiunta Libri**: Clicca su **"Aggiungi Nuovo Libro"** per aprire il modulo di registrazione.
- **Integrazione Google Books**: Inserisci un titolo e clicca su **"Cerca tramite API"** per recuperare automaticamente i metadati (descrizione, anno, autori) dai record globali.
- **Modifica Record**: Clicca su qualsiasi titolo nell'elenco per modificarne i dettagli, incluse le categorie e le note d'archivio.

<a name="scansione-barcode"></a>
### Scansione Barcode
Il sistema supporta scanner hardware fisici o la scansione tramite fotocamera.
- Nel modulo di aggiunta libro, clicca sull'icona della fotocamera **"Scansiona"**.
- Posiziona il codice a barre del libro nel riquadro. Il sistema catturerà l'ISBN e ti permetterà di **"Autocompletare"** i metadati immediatamente.

<a name="registro-autori"></a>
### Registro Autori
Gestisci il database dei creatori nel **Gestore Autori**.
- **Ricerca Wikipedia**: Quando aggiungi un autore, usa lo strumento **"Scopri Ritratto"** per recuperare automaticamente un'immagine del profilo e un link biografico da Wikipedia.
- **Sigillare Record**: I profili degli autori possono essere **Sigillati** (bloccati) per prevenire modifiche accidentali durante le revisioni amministrative.

<a name="operazioni-massive"></a>
### Operazioni Massive
Risparmia tempo con gli aggiornamenti di massa:
- Seleziona più libri nel registro del **Gestore Libri**.
- Clicca su **"Modifica Massiva"** per aggiornare simultaneamente anni di pubblicazione, categorie o stato di disponibilità per l'intera selezione.

<a name="importazione-ed-esportazione"></a>
### Importazione ed Esportazione Archivio
Il **Gestore Libri** offre potenti strumenti per la gestione massiva dei dati.
- **Esportazione dell'Archivio**: Usa il menu a discesa **"Esporta"** per scaricare l'intero catalogo della biblioteca nei formati **JSON** o **CSV**. Questo è ideale per backup o analisi esterne.
- **Importazione Massiva via ISBN**: 
    - Clicca su **"Importa"** per aprire lo strumento di registrazione massiva.
    - Incolla un elenco di codici ISBN (uno per riga) o carica un file di testo.
    - **Risoluzione Intelligente**: Il sistema ricerca automaticamente i metadati per ogni ISBN tramite API globali.
    - **Monitoraggio Avanzamento**: Una barra di avanzamento in tempo reale mostra lo stato corrente e il tempo stimato rimanente per le importazioni di grandi dimensioni.

---

<a name="gestione-utenti"></a>
## 6. Amministrazione del Sistema

### Gestione Utenti
Gli amministratori gestiscono gli accessi tramite il **Gestore Utenti**.
- **Assegnazione Ruoli**: Promuovi gli utenti ai ruoli di **Manager** o **Bibliotecario** per concedere l'accesso al catalogo.
- **Controllo Account**: Cerca gli utenti per email o nome utente per revisionare il loro stato e i loro ruoli.

### Anonimizzazione GDPR e "Diritto all'Oblio"
Gli utenti possono richiedere la cancellazione dell'account tramite le impostazioni del profilo (**Gestisci i tuoi dati**).
- **Flusso di Anonimizzazione**: Per conformarsi al GDPR preservando al contempo i record storici della biblioteca (come prestiti e recensioni passati), il sistema esegue l'**Anonimizzazione** invece della cancellazione fisica.
- **Disabilitazione Account**: I dettagli personali dell'utente (Email, Nome, Codice Fiscale) vengono cancellati definitivamente e sostituiti con identificatori casuali. L'account viene quindi bloccato in modo permanente.
- **Preservazione dei Record**: Tutte le interazioni storiche (es. che un libro è stato preso in prestito dall'*Utente X*) rimangono intatte per l'integrità dell'archivio, ma l'*Utente X* non è più identificabile.

<a name="affidabilità-del-servizio-email"></a>
### Affidabilità del Servizio Email
Il sistema include un servizio dedicato di **Re-invio Email in Background**.
- Se un'email di notifica (come la conferma dell'account o gli avvisi di prestito) fallisce a causa di problemi del servizio esterno, il sistema la mette automaticamente in coda.
- Un worker in background riprova l'invio delle email fallite ogni 10 minuti fino al successo, garantendo che nessuna comunicazione critica vada persa.

---

<a name="risoluzione-dei-problemi-e-supporto"></a>
## 7. Risoluzione dei Problemi e Supporto

| Problema | Possibile Soluzione |
| :--- | :--- |
| **Email non ricevuta** | Controlla la cartella Spam. Se non è ancora arrivata, il worker in background riproverà l'invio automaticamente. |
| **Scansione fallita** | Assicurati che ci sia un'illuminazione adeguata e che il codice a barre sia pulito. In alternativa, digita l'ISBN manualmente e usa il pulsante **Cerca**. |
| **Impossibile modificare record** | Controlla se il record è **"Sigillato"**. Un Manager o Amministratore deve dissigillarlo prima che gli aggiornamenti possano essere applicati. |
| **Nessun risultato da Google Books** | Verifica l'ortografia del titolo o prova a cercare per ISBN per una corrispondenza più precisa. |

---
*Per i dettagli tecnici sull'architettura, consulta [Architecture.md](file:///Users/antoniolucca/github/blazorBookLibrary/Docs/Architecture.md).*
