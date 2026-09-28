# CloudKnowledge.Api

Eine kleine **Retrieval-Augmented-Generation-Demo (RAG)** mit ASP.NET Core und Azure OpenAI.

Die API beantwortet technische Fragen zu einem fiktiven SaaS-Produkt namens **CloudStore**. Sie antwortet
ausschließlich auf Basis einer lokalen Markdown-Wissensbasis, nicht aus dem allgemeinen Wissen des Modells,
und zeigt zu jeder Antwort, aus welchen Dokumenten sie stammt.

> **Stand: Phase 1, Meilenstein M2 (Retrieval) umgesetzt.** Die API baut beim Start einen durchsuchbaren Index
> aus der Dokumentation und beantwortet Suchanfragen über `GET /api/knowledge/search`. Offen ist noch der erste Lauf
> gegen ein echtes Azure OpenAI. Antwortgenerierung (M3) und Evaluation (M4) folgen. Dieses README beschreibt
> sowohl den **heutigen Stand** als auch das **Ziel** am Ende von Phase 1, damit man beides vergleichen kann.

- [`SPEC.md`](SPEC.md): die vollständige Spezifikation und maßgebliche Quelle.
- [`tickets/`](tickets/README.md): die Umsetzungs-Tickets, das Validation log und das Replanning log.
- [`CLAUDE.md`](CLAUDE.md): die spec-getriebene Arbeitsweise.

---

## 1. Die Idee in einem Bild

Ein Sprachmodell formuliert gute Antworten, kennt aber *unsere* Dokumentation nicht. RAG löst das: Bevor das
Modell antwortet, **sucht** die API die passenden Stellen in der Dokumentation heraus und gibt sie dem Modell
als einzige Quelle mit.

```mermaid
flowchart LR
    Q["Frage<br/>'HTTP 503 nach dem Deployment?'"] --> R["1 · Retrieve<br/>die passendsten<br/>Doku-Abschnitte finden"]
    R --> G["2 · Generate<br/>das Modell antwortet nur<br/>auf Basis dieser Abschnitte"]
    G --> A["Antwort + Quellen<br/>[1] troubleshooting.md<br/>[2] monitoring.md"]
    KB[("CloudStore-Doku<br/>9 Markdown-Dateien")] -.-> R
```

Retrieval und Generierung sind zwei getrennte Schritte, und die API wird sie auch getrennt anbieten:
`/search` zeigt nur, was gefunden wurde, `/ask` erzeugt zusätzlich eine Antwort.

---

## 2. Aktuelle Architektur (nach M2)

### 2.1 Was es gibt und was es tut

| Baustein | Stand | Aufgabe |
|---|---|---|
| Solution mit 3 Projekten und Tests | ✅ gebaut | Teilt den Code in klare Schichten (siehe 2.2) |
| Konfiguration (Options-Klassen) | ✅ gebaut | Liest die Einstellungen und **bricht den Start ab**, wenn eine fehlt oder ungültig ist |
| Wissensbasis `docs/` + Guard-Test | ✅ gebaut | 9 CloudStore-Dokumente mit gezielt eingebauten Testfällen |
| Chunker | ✅ gebaut | Zerlegt jedes Dokument in Abschnitte (ein Chunk pro H2-Überschrift, heute 67 Chunks) |
| Embedding-Cache | ✅ gebaut | Merkt sich berechnete Vektoren in einer JSON-Datei, damit Azure jeden Text nur einmal sieht |
| Indexer + Vector-Store | ✅ gebaut | Baut beim Start und auf Anfrage den durchsuchbaren Index im Speicher |
| Health-Endpoints | ✅ gebaut | `/health/ready` wird grün, sobald der Index steht |
| `GET /search`, `POST /index` | ✅ gebaut | Suche ohne Sprachmodell, Index neu aufbauen |
| OpenAPI + Scalar UI | ✅ gebaut | API-Dokumentation im Browser (nur in Development) |
| Lauf gegen echtes Azure OpenAI | ⏳ offen | Demo-Schritt 2 mit echten Embeddings, liefert die ersten echten Scores |
| `/ask`, Evaluation | ⏳ geplant | Meilensteine M3 und M4 |

### 2.2 Projekte und ihre Abhängigkeiten

Der Code ist auf drei Projekte verteilt. Die Pfeile bedeuten „nutzt“. Die wichtigste Regel: **Application
weiß nichts von Azure.** Sie kennt nur Abstraktionen, die `Infrastructure` mit echter Technik füllt. Deshalb
lässt sich die Kernlogik ohne Cloud-Zugang testen, und ein Test prüft diese Regel.

```mermaid
flowchart TB
    Api["<b>CloudKnowledge.Api</b><br/>Endpoints /search · /index · Health<br/>Indexieren beim Start · OpenAPI"]
    Infra["<b>CloudKnowledge.Infrastructure</b><br/>AddCloudKnowledge() · Azure OpenAI<br/>Dateien · Embedding-Cache · Vector-Store"]
    App["<b>CloudKnowledge.Application</b><br/>Chunker · Indexer · Suche · IndexState<br/><i>keine Azure-Abhängigkeit</i>"]
    Tests["<b>CloudKnowledge.Tests</b><br/>Unit- + Integrationstests<br/>Fake-AI-Clients"]

    Api --> Infra
    Infra --> App
    Tests -.-> Api
    Tests -.-> Infra
    Tests -.-> App
```

| Projekt | Enthält heute |
|---|---|
| `src/CloudKnowledge.Api` | `Program.cs`, `Endpoints/KnowledgeEndpoints.cs`, `Indexing/KnowledgeIndexingService.cs`, `Health/*`, `CloudKnowledge.Api.http` |
| `src/CloudKnowledge.Application` | `Configuration/*Options.cs`, `Chunking/MarkdownChunker.cs`, `Indexing/` (`IndexState`, `KnowledgeIndexer`, `EmbeddingService`, `KnowledgeChunk`, Schnittstellen), `Search/KnowledgeSearch.cs` |
| `src/CloudKnowledge.Infrastructure` | `AddCloudKnowledge()`, `Knowledge/` (`FileDocumentSource`, `JsonEmbeddingCache`, `KnowledgePaths`) |
| `tests/CloudKnowledge.Tests` | Test-Host `CloudKnowledgeApiFactory`, Fakes, 106 Tests |

### 2.3 Was beim Start der API passiert

```mermaid
sequenceDiagram
    autonumber
    participant P as Program.cs
    participant O as Options-Validierung
    participant S as Indexieren beim Start
    participant X as KnowledgeIndexer
    participant H as /health/ready

    P->>O: App startet und prüft alle Options
    alt eine Einstellung fehlt oder ist ungültig
        O-->>P: Abbruch mit klarer Meldung<br/>"AzureOpenAI:Endpoint is required…"
    else alle Einstellungen gültig
        O-->>P: API nimmt sofort Anfragen an
        P->>S: Hintergrunddienst starten
        S->>X: Index aufbauen (siehe 2.4)
        Note over H: währenddessen 503 "Indexing"
        alt Indexieren klappt
            X-->>H: Ready → 200
        else Fehler, z. B. kein az login
            X-->>H: Failed → 503 mit Fehlermeldung
            Note over S: die API läuft trotzdem weiter
        end
    end
```

Weil die Konfiguration schon beim Start geprüft wird (**fail fast**), fällt eine fehlende Einstellung sofort
auf. Ein Fehler beim Indexieren bringt die API dagegen nicht zum Absturz: `/health/live` bleibt grün, und
`/health/ready` zeigt, was schiefging.

### 2.4 So wird der Index aufgebaut

Beim Start und bei jedem Aufruf von `POST /api/knowledge/index` wird die Dokumentation in durchsuchbare Vektoren
umgewandelt. Bereits berechnete Embeddings kommen aus dem Cache, ein Neustart kostet deshalb fast nichts.

```mermaid
flowchart LR
    Docs[("docs/*.md<br/>9 Dateien")] --> Chunk["MarkdownChunker<br/>ein Chunk pro<br/>H2-Abschnitt"]
    Chunk --> Cache{"Embedding<br/>im Cache?"}
    Cache -- ja --> Store[("In-Memory-<br/>Vector-Store<br/>67 Chunks")]
    Cache -- "nein (alle fehlenden<br/>in einem Aufruf)" --> Embed["Azure OpenAI<br/>text-embedding-3-small"]
    Embed --> Store
    Store --> Ready["IndexState = Ready<br/>/health/ready → 200"]
```

- **Chunks:** Jeder Chunk ist ein H2-Abschnitt, zum Beispiel *troubleshooting.md > HTTP 503 after deployment*.
  Zu lange Abschnitte würden an Leerzeilen geteilt; bei den heutigen Dokumenten ist das nie nötig.
- **Cache:** Der Schlüssel ist ein Hash aus Deployment-Name und Text. Wechselt das Embedding-Modell, wird
  automatisch neu berechnet.
- **Neu indexieren:** Die Collection wird geleert und neu gefüllt. Solange liefern `/search` und `/health/ready`
  503. Mit Cache dauert das Bruchteile einer Sekunde. Läuft schon ein Durchlauf, antwortet `POST /index` mit 409.

### 2.5 So funktioniert die Suche (`GET /search`)

Die Suche ist der **Retrieval-Teil** von RAG, ganz ohne Sprachmodell. Sie zeigt, was `/ask` später dem Modell
mitgeben wird.

```mermaid
flowchart LR
    Q["q = 'How does the deployment<br/>process work?'"] --> E["Frage in einen<br/>Vektor umwandeln"]
    E --> V[("Vector-Store")]
    V --> R["Top-k Chunks nach<br/>Cosinus-Ähnlichkeit"]
    R --> M["jeder Treffer markiert:<br/>aboveThreshold = score ≥ MinScore"]
```

```json
{ "query": "How does the deployment process work?",
  "minScore": 0.0,
  "results": [ { "document": "deployment.md", "section": "Pipeline stages", "score": 0.62,
                 "aboveThreshold": true, "content": "..." } ] }
```

Die Treffer kommen **unabhängig** von `MinScore` zurück, sind aber markiert. So lässt sich die Schwelle später
anhand echter Scores einstellen. (Die Zahlen oben sind ein Beispiel; echte Scores gibt es erst mit Azure.)

### 2.6 Konfiguration

Alle Einstellungen sind typisierte Klassen und werden beim Start geprüft:

| Abschnitt | Einstellungen | Prüfung |
|---|---|---|
| `AzureOpenAI` | `Endpoint`, `ChatDeployment`, `EmbeddingDeployment` | Pflichtfelder, Endpoint muss `https` sein |
| `Rag` | `TopK` (5), `MinScore` (0.0) | 1–20, 0.0–1.0 |
| `Chunking` | `MaxTokens` (400) | 50–2000 |
| `Knowledge` | `DocsPath`, `EmbeddingCachePath`, `IndexOnStartup` (true) | Pflichtfelder |

Die echten Werte (Endpoint und Deployment-Namen) liegen als **User Secrets** auf dem Entwicklerrechner und
werden nie committet. `appsettings.json` enthält nur leere Werte und Standardwerte.

`docs/` wird beim Build in den Ausgabeordner kopiert. Relative Pfade gelten ab diesem Ordner, deshalb finden
API und Tests dieselben Dateien, egal aus welchem Verzeichnis sie gestartet werden.

### 2.7 Authentifizierung bei Azure OpenAI: ohne Keys

Die API verwendet keinen API-Key. Sie weist sich über **Microsoft Entra ID** mit `DefaultAzureCredential`
aus. Auf dem Laptop ist das der `az login` des Entwicklers. In Azure würde derselbe Code ohne jede Änderung
eine **Managed Identity** verwenden.

```mermaid
flowchart LR
    subgraph local["Lokale Entwicklung"]
        Dev["az login<br/>(Entwickler-Konto)"]
    end
    subgraph azure["Würde in Azure genauso funktionieren"]
        MI["Managed Identity<br/>des Containers"]
    end
    Dev --> DAC["Azure-OpenAI-Client<br/>mit DefaultAzureCredential"]
    MI -.-> DAC
    DAC -->|"1 · Access Token holen"| Entra["Microsoft Entra ID"]
    DAC -->|"2 · Aufruf mit Token"| AOAI["Azure OpenAI<br/>Chat- + Embedding-Deployments"]
```

Die angemeldete Identität braucht auf der Azure-OpenAI-Ressource die Rolle **Cognitive Services OpenAI User**.

### 2.8 Health-Endpoints und Index-Zustand

Die API hat zwei Health-Endpoints mit unterschiedlicher Bedeutung:

- **`/health/live`**: *Läuft der Prozess?* Solange die API läuft, immer 200.
- **`/health/ready`**: *Kann die API Fragen beantworten?* Nur 200, wenn der Wissensindex aufgebaut ist.

Die Antwort von `/health/ready` kommt aus `IndexState`, den der Indexer weiterschaltet:

```mermaid
stateDiagram-v2
    [*] --> NotStarted
    NotStarted --> Indexing: Start der API oder POST /index
    Indexing --> Ready: alle Chunks indexiert
    Indexing --> Failed: Fehler
    Ready --> Indexing: POST /index
    Failed --> Indexing: POST /index

    note right of Ready: /health/ready = 200
    note right of Failed: /health/ready = 503<br/>mit Fehlermeldung
```

Beispielantwort, sobald der Index steht:

```json
{ "status": "Healthy",
  "checks": [ { "name": "index", "status": "Healthy", "description": "Index ready with 67 chunks." } ] }
```

Das spiegelt das fiktive CloudStore wider, dessen eigene `monitoring.md` dasselbe Live/Ready-Muster beschreibt.

### 2.9 Die Wissensbasis

`docs/` beschreibt CloudStore, ein fiktives SaaS für Dokumentenverwaltung (Angular, ASP.NET Core, PostgreSQL,
Blob Storage, Container Apps, Entra ID). CloudStore existiert nur in diesen Dokumenten. Die Dokumente sind auf
Englisch und bewusst so geschrieben, dass die Demo drei Dinge zeigen kann:

```mermaid
flowchart TB
    subgraph split["1 · Antworten, die mehrere Dokumente brauchen"]
        direction LR
        S1["storage.md<br/>Uploads → Blob Storage"] --- Q1(("Wo werden Uploads<br/>gespeichert, und wie<br/>authentifiziert sich<br/>die App dort?")) --- S2["security.md<br/>Blob-Zugriff per<br/>Managed Identity"]
        T1["troubleshooting.md<br/>503: was prüfen?"] --- Q2(("HTTP 503 nach<br/>dem Deployment?")) --- T2["monitoring.md<br/>/health/ready"]
    end
    subgraph missing["2 · Fakten, die bewusst fehlen"]
        M["max. Upload-Größe · SLA · Backup-Aufbewahrung"]
    end
    subgraph bait["3 · Köder"]
        B["storage.md schreibt viel über Uploads,<br/>nennt aber nie ein Limit"]
    end
```

Ein **Guard-Test** (`KnowledgeBaseTests`) stellt sicher, dass die fehlenden Fakten fehlen und die verteilten
Fakten verteilt bleiben. Schreibt jemand „Files up to 100 MB…“ in die Doku, schlägt der Test fehl und nennt
die Datei.

---

## 3. Zielarchitektur (Ende von Phase 1)

### 3.1 Komponenten

Grüne Teile gibt es schon (M1 und M2). Graue Teile sind noch geplant, in Klammern steht der Meilenstein, der sie bringt.
Die Pfeile bedeuten „ruft auf“ oder „nutzt“.

```mermaid
flowchart TB
    User(["Entwickler · Scalar UI / .http-Dateien"])

    subgraph api["CloudKnowledge.Api — HTTP-Endpoints"]
        direction LR
        Health["/health/live<br/>/health/ready"]
        Index["POST /index<br/>+ Indexieren beim Start"]
        Search["GET /search"]
        Ask["POST /ask<br/>(M3)"]
    end

    subgraph app["CloudKnowledge.Application — Kernlogik, kein Azure"]
        direction LR
        State["IndexState"]
        Indexer["KnowledgeIndexer<br/>+ MarkdownChunker<br/><i>siehe 2.4</i>"]
        KSearch["KnowledgeSearch"]
        Rag["RagService<br/>MinScore-Schwelle · Prompt · Zitate<br/>(M3)"]
    end

    subgraph infra["CloudKnowledge.Infrastructure — spricht mit der Außenwelt"]
        direction LR
        Files["Dokumentquelle docs/<br/>+ Embedding-Cache"]
        Store[("In-Memory-<br/>Vector-Store")]
        Clients["Azure-OpenAI-Clients<br/>Chat · Embeddings"]
    end

    AOAI["Azure OpenAI"]

    User --> api
    Health --> State
    Index --> Indexer
    Search --> KSearch
    Ask --> Rag
    Indexer --> Files
    Indexer --> Store
    Indexer --> Clients
    KSearch --> Store
    KSearch --> Clients
    Rag --> KSearch
    Rag --> Clients
    Clients --> AOAI

    classDef built fill:#d4edda,stroke:#2e7d32,color:#1b3d1f
    classDef planned fill:#eeeeee,stroke:#9e9e9e,color:#555555,stroke-dasharray: 4 3
    class Health,State,Clients,Index,Search,Indexer,KSearch,Files,Store built
    class Ask,Rag planned
```

### 3.2 So wird eine Frage beantwortet (M3)

`/ask` nutzt für die Suche genau dieselbe `KnowledgeSearch` wie `/search`. Was `/search` zeigt, ist also genau das,
was das Modell zu sehen bekommt.

```mermaid
sequenceDiagram
    autonumber
    actor U as Nutzer
    participant API as POST /ask
    participant S as KnowledgeSearch<br/>(wie GET /search)
    participant C as Chat-Modell

    U->>API: "HTTP 503 nach dem Deployment – was soll ich prüfen?"
    API->>S: die 5 ähnlichsten Chunks suchen
    S-->>API: Chunks mit Scores und aboveThreshold
    alt kein Chunk erreicht MinScore
        API-->>U: answered: false<br/>„Die Dokumentation enthält das nicht.“<br/>(das Chat-Modell wird nicht aufgerufen)
    else passende Chunks gefunden
        API->>C: Prompt = Regeln + nummerierte Chunks [1]..[n] + Frage
        C-->>API: Antwort mit Markern [1] [2]
        API-->>U: Antwort + Zitate + gefundene Quellen
    end
```

Es gibt **zwei Schutzschichten gegen Halluzinationen**:

1. **Deterministisch:** Wird nichts Passendes gefunden, wird das Modell gar nicht erst gefragt.
2. **Anweisung:** Der Prompt verlangt, dass das Modell nur den mitgegebenen Kontext nutzt und es offen sagt,
   wenn die Antwort dort nicht steht.

### 3.3 Ist und Ziel im Überblick

| Bereich | Heute (M2) | Ziel (Ende Phase 1) | Meilenstein |
|---|---|---|---|
| Solution-Struktur | Api, Application, Infrastructure, Tests | + `CloudKnowledge.Eval` | M4 |
| Konfiguration | typisiert, beim Start geprüft ✅ | unverändert | ✅ |
| Azure OpenAI | Embeddings angebunden (Lauf gegen echtes Azure offen) | + Chat | M3 |
| Health | live ✅, ready 200 nach dem Indexieren ✅ | unverändert | ✅ |
| Wissensbasis | 9 Dokumente + Guard-Test ✅ | unverändert | ✅ |
| Chunking | ein Chunk pro H2-Abschnitt, 67 Chunks ✅ | unverändert | ✅ |
| Vector-Store | In-Memory (VectorData-Abstraktion) ✅ | unverändert | ✅ |
| Endpoints | Health, `/search`, `/index` ✅ | + `/ask` | M3 |
| API-Doku | OpenAPI + Scalar UI ✅ | unverändert | ✅ |
| Schutz vor Halluzinationen | `MinScore` markiert Suchtreffer (Platzhalter 0.0) | `MinScore`-Schwelle + Prompt-Regeln + Zitate | M3 |
| Qualitätsmessung | keine | Recall@k, kalibrierter `MinScore` | M4 |

---

## 4. Lokal starten

**Voraussetzungen:** .NET 10 SDK, Azure CLI und eine Azure-OpenAI-Ressource mit einem Chat-Deployment und
einem `text-embedding-3-small`-Deployment. Dein Konto braucht auf dieser Ressource die Rolle
**Cognitive Services OpenAI User**.

```bash
az login

dotnet user-secrets set "AzureOpenAI:Endpoint" "https://<deine-ressource>.openai.azure.com/" --project src/CloudKnowledge.Api
dotnet user-secrets set "AzureOpenAI:ChatDeployment" "<dein-chat-deployment>" --project src/CloudKnowledge.Api

dotnet build
dotnet test          # ruft nie Azure auf
dotnet run --project src/CloudKnowledge.Api
```

Die API läuft auf `http://localhost:5210`. Nach dem Indexieren (wenige Sekunden) liefert `/health/ready` 200.
Die Demo-Anfragen stehen in [`src/CloudKnowledge.Api/CloudKnowledge.Api.http`](src/CloudKnowledge.Api/CloudKnowledge.Api.http),
die API-Dokumentation zum Ausprobieren unter `http://localhost:5210/scalar`.

`POST /api/knowledge/index` hat **keine Authentifizierung**: Die API ist nur für den lokalen Betrieb gedacht.

---

## 5. Die wichtigsten Entscheidungen bisher

| Entscheidung | Warum |
|---|---|
| Läuft nur lokal, kein Deployment nach Azure | Hält den Umfang klein. Managed Identity wird erklärt, nicht deployt. |
| `DefaultAzureCredential`, keine API-Keys | Keine Secrets im Repo; derselbe Code funktioniert lokal und in Azure. |
| Drei Projekte, `Application` ohne Azure | Die Kernlogik ist ohne Cloud-Zugang testbar. |
| Konfiguration beim Start prüfen | Fehler fallen sofort auf, mit klarer Meldung. |
| Getrennte Live- und Ready-Endpoints | „Prozess läuft“ und „kann Fragen beantworten“ sind zwei verschiedene Dinge. |
| Wissensbasis mit verteilten, fehlenden und Köder-Fakten | Zeigt Retrieval über mehrere Dokumente und wie das System mit unbeantwortbaren Fragen umgeht. |
| Guard-Test für die Wissensbasis | Die Demo-Fälle können nicht unbemerkt kaputtgehen. |
| Suche (`/search`) getrennt von der Antwort (`/ask`) | Retrieval lässt sich zeigen und messen, bevor ein Sprachmodell beteiligt ist. |
| Embedding-Cache mit Deployment-Name im Schlüssel | Neustarts kosten nichts; ein anderes Modell rechnet automatisch neu. |
| Neu indexieren = leeren und neu füllen, solange 503 | Mit Cache dauert das Bruchteile einer Sekunde; einfacher als ein Austausch im Hintergrund. |
| Tests nutzen standardmäßig Fake-AI-Clients | Kein Test ruft Azure auf, auch nicht das Indexieren beim Start. |
| Preview-Connector für den Vector-Store, Abstraktion auf seine Version gepinnt | Stabile Schnittstelle, austauschbarer Connector; die neueste Abstraktion ist mit ihm nicht kompatibel. |

Alle Entscheidungen, auch die geplanten, stehen im [Decision Log in SPEC.md](SPEC.md#20-decision-log).
