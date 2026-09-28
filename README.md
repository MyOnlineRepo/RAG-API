# CloudKnowledge.Api

Eine kleine **Retrieval-Augmented-Generation-Demo (RAG)** mit ASP.NET Core und Azure OpenAI.

Die API beantwortet technische Fragen zu einem fiktiven SaaS-Produkt namens **CloudStore**. Sie antwortet
ausschließlich auf Basis einer lokalen Markdown-Wissensbasis, nicht aus dem allgemeinen Wissen des Modells,
und zeigt zu jeder Antwort, aus welchen Dokumenten sie stammt.

> **Stand: Phase 1, Meilenstein M1 (Grundgerüst) abgeschlossen.** Grundgerüst, Konfiguration, Anbindung an
> Azure OpenAI, Health-Endpoints und Wissensbasis sind fertig. Retrieval (M2), Antwortgenerierung (M3) und
> Evaluation (M4) folgen noch. Dieses README beschreibt sowohl den **heutigen Stand** als auch das **Ziel**
> am Ende von Phase 1, damit man beides vergleichen kann.

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

## 2. Aktuelle Architektur (nach M1)

### 2.1 Was es gibt und was es tut

| Baustein | Stand | Aufgabe |
|---|---|---|
| Solution mit 3 Projekten und Tests | ✅ gebaut | Teilt den Code in klare Schichten (siehe 2.2) |
| Konfiguration (Options-Klassen) | ✅ gebaut | Liest die Einstellungen und **bricht den Start ab**, wenn eine fehlt oder ungültig ist |
| Azure-OpenAI-Clients | ✅ registriert, ⏳ noch nicht genutzt | Chat- und Embedding-Client stehen für M2 und M3 bereit |
| Index-Zustand + Health-Endpoints | ✅ gebaut | `/health/live` und `/health/ready` melden, ob die API läuft und ob sie bereit ist |
| Wissensbasis `docs/` | ✅ geschrieben und freigegeben | 9 CloudStore-Dokumente mit gezielt eingebauten Testfällen |
| Guard-Test für die Wissensbasis | ✅ gebaut | Stellt sicher, dass spätere Änderungen die Demo-Fälle nicht kaputt machen |
| Retrieval, `/search`, `/ask`, Evaluation | ⏳ geplant | Meilensteine M2–M4 |

### 2.2 Projekte und ihre Abhängigkeiten

Der Code ist auf drei Projekte verteilt. Die Pfeile bedeuten „nutzt“. Die wichtigste Regel: **Application
weiß nichts von Azure.** Deshalb lässt sich die Kernlogik ohne Cloud-Zugang testen.

```mermaid
flowchart TB
    Api["<b>CloudKnowledge.Api</b><br/>Web-Host · Program.cs<br/>Health-Endpoints"]
    Infra["<b>CloudKnowledge.Infrastructure</b><br/>AddCloudKnowledge()<br/>Anbindung an Azure OpenAI"]
    App["<b>CloudKnowledge.Application</b><br/>Options-Klassen · IndexState<br/><i>keine Azure-Abhängigkeit</i>"]
    Tests["<b>CloudKnowledge.Tests</b><br/>Unit- + Integrationstests<br/>Fake-AI-Clients"]

    Api --> Infra
    Infra --> App
    Tests -.-> Api
    Tests -.-> Infra
    Tests -.-> App
```

| Projekt | Enthält heute |
|---|---|
| `src/CloudKnowledge.Api` | `Program.cs`, `Health/IndexReadinessHealthCheck.cs`, `Health/HealthEndpoints.cs` |
| `src/CloudKnowledge.Application` | `Configuration/*Options.cs`, `Indexing/IndexState.cs` |
| `src/CloudKnowledge.Infrastructure` | `ServiceCollectionExtensions.cs` mit `AddCloudKnowledge()` |
| `tests/CloudKnowledge.Tests` | Test-Host `CloudKnowledgeApiFactory`, Fakes, 46 Tests |

### 2.3 Was beim Start der API passiert

```mermaid
sequenceDiagram
    autonumber
    participant P as Program.cs
    participant DI as AddCloudKnowledge()
    participant O as Options-Validierung
    participant H as Health-Endpoints

    P->>DI: Options, IndexState und AI-Clients registrieren
    Note over DI: AI-Clients werden erst bei Bedarf erzeugt,<br/>es gibt also noch keinen Aufruf an Azure
    P->>H: /health/live und /health/ready einrichten
    P->>O: App startet und prüft alle Options
    alt eine Einstellung fehlt oder ist ungültig
        O-->>P: Abbruch mit klarer Meldung<br/>"AzureOpenAI:Endpoint is required…"
    else alle Einstellungen gültig
        O-->>P: API nimmt Anfragen an
    end
```

Weil die Konfiguration schon beim Start geprüft wird (**fail fast**), fällt eine fehlende Einstellung sofort
auf und nicht erst bei der ersten Anfrage.

### 2.4 Konfiguration

Alle Einstellungen sind typisierte Klassen und werden beim Start geprüft:

| Abschnitt | Einstellungen | Prüfung |
|---|---|---|
| `AzureOpenAI` | `Endpoint`, `ChatDeployment`, `EmbeddingDeployment` | Pflichtfelder, Endpoint muss `https` sein |
| `Rag` | `TopK` (5), `MinScore` (0.0) | 1–20, 0.0–1.0 |
| `Chunking` | `MaxTokens` (400) | 50–2000 |
| `Knowledge` | `DocsPath`, `EmbeddingCachePath` | Pflichtfelder |

Die echten Werte (Endpoint und Deployment-Namen) liegen als **User Secrets** auf dem Entwicklerrechner und
werden nie committet. `appsettings.json` enthält nur leere Werte und Standardwerte.

### 2.5 Authentifizierung bei Azure OpenAI: ohne Keys

Die API verwendet keinen API-Key. Sie weist sich über **Microsoft Entra ID** mit `DefaultAzureCredential`
aus. Auf dem Laptop ist das der `az login` des Entwicklers. In Azure würde derselbe Code ohne jede Änderung
eine **Managed Identity** verwenden.

```mermaid
flowchart LR
    subgraph local["Heute: lokale Entwicklung"]
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

### 2.6 Health-Endpoints und Index-Zustand

Die API hat zwei Health-Endpoints mit unterschiedlicher Bedeutung:

- **`/health/live`**: *Läuft der Prozess?* Solange die API läuft, immer 200.
- **`/health/ready`**: *Kann die API Fragen beantworten?* Nur 200, wenn der Wissensindex aufgebaut ist.

Die Antwort von `/health/ready` kommt aus `IndexState`. Heute baut noch nichts einen Index auf. Der Zustand
bleibt deshalb auf `NotStarted`, und `/health/ready` liefert korrekterweise **503**. Mit M2 kommt das
Indexieren hinzu, das den Zustand weiterschaltet.

```mermaid
stateDiagram-v2
    [*] --> NotStarted
    NotStarted --> Indexing: Indexieren startet (M2)
    Indexing --> Ready: alle Chunks indexiert
    Indexing --> Failed: Fehler
    Ready --> Indexing: neu indexieren (POST /index, M2)
    Failed --> Indexing: erneuter Versuch

    note right of NotStarted: heute bleibt der Zustand hier<br/>→ /health/ready = 503
    note right of Ready: /health/ready = 200
```

Beispielantwort, solange die API nicht bereit ist:

```json
{ "status": "Unhealthy",
  "checks": [ { "name": "index", "status": "Unhealthy", "description": "Index status: NotStarted." } ] }
```

Das spiegelt das fiktive CloudStore wider, dessen eigene `monitoring.md` dasselbe Live/Ready-Muster beschreibt.

### 2.7 Die Wissensbasis

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

Grüne Teile gibt es schon. Graue Teile sind noch geplant, in Klammern steht der Meilenstein, der sie bringt.
Die Pfeile bedeuten „ruft auf“ oder „nutzt“.

```mermaid
flowchart TB
    User(["Entwickler · Scalar UI / .http-Dateien"])

    subgraph api["CloudKnowledge.Api — HTTP-Endpoints"]
        direction LR
        Health["/health/live<br/>/health/ready"]
        Index["POST /index<br/>+ Indexieren beim Start<br/>(M2)"]
        Search["GET /search<br/>(M2)"]
        Ask["POST /ask<br/>(M3)"]
    end

    subgraph app["CloudKnowledge.Application — Kernlogik, kein Azure"]
        direction LR
        State["IndexState"]
        Indexer["KnowledgeIndexer<br/>+ MarkdownChunker<br/>(M2) · <i>siehe 3.2</i>"]
        KSearch["KnowledgeSearch<br/>(M2)"]
        Rag["RagService<br/>MinScore-Schwelle · Prompt · Zitate<br/>(M3)"]
    end

    subgraph infra["CloudKnowledge.Infrastructure — spricht mit der Außenwelt"]
        direction LR
        Files["Dokumentquelle docs/<br/>+ Embedding-Cache<br/>(M2)"]
        Store[("In-Memory-<br/>Vector-Store (M2)")]
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
    class Health,State,Clients built
    class Index,Search,Ask,Indexer,KSearch,Rag,Files,Store planned
```

### 3.2 So wird der Index aufgebaut (M2)

Beim Start und bei jedem Aufruf von `POST /index` wird die Dokumentation in durchsuchbare Vektoren umgewandelt.
Bereits berechnete Embeddings kommen aus einem lokalen Cache, ein Neustart kostet deshalb fast nichts.

```mermaid
flowchart LR
    Docs[("docs/*.md")] --> Chunk["In Chunks zerlegen<br/>ein Chunk pro H2-Abschnitt"]
    Chunk --> Cache{"Embedding<br/>im Cache?"}
    Cache -- ja --> Store[("Vector-Store")]
    Cache -- nein --> Embed["Azure OpenAI<br/>Embedding-Modell"]
    Embed --> Store
    Store --> Ready["IndexState = Ready<br/>/health/ready → 200"]
```

Jeder Chunk ist ein H2-Abschnitt eines Dokuments, zum Beispiel *troubleshooting.md > HTTP 503 after deployment*.
Ganze Abschnitte liefern präzise Suchtreffer und gut lesbare Quellenangaben.

### 3.3 So wird eine Frage beantwortet (M3)

```mermaid
sequenceDiagram
    autonumber
    actor U as Nutzer
    participant API as POST /ask
    participant E as Embedding-Modell
    participant V as Vector-Store
    participant C as Chat-Modell

    U->>API: "HTTP 503 nach dem Deployment – was soll ich prüfen?"
    API->>E: Frage in einen Vektor umwandeln
    API->>V: die 5 ähnlichsten Chunks suchen
    V-->>API: Chunks mit Scores
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

### 3.4 Ist und Ziel im Überblick

| Bereich | Heute (M1) | Ziel (Ende Phase 1) | Meilenstein |
|---|---|---|---|
| Solution-Struktur | Api, Application, Infrastructure, Tests | + `CloudKnowledge.Eval` | M4 |
| Konfiguration | typisiert, beim Start geprüft | unverändert | ✅ |
| Azure OpenAI | Clients registriert, nicht aufgerufen | Embeddings (M2), Chat (M3) | M2, M3 |
| Health | live ✅, ready immer 503 | ready wird nach dem Indexieren 200 | M2 |
| Wissensbasis | 9 Dokumente + Guard-Test ✅ | unverändert | ✅ |
| Chunking | keins | ein Chunk pro H2-Abschnitt | M2 |
| Vector-Store | keiner | In-Memory (VectorData-Abstraktion) | M2 |
| Endpoints | nur Health | `/search`, `/index`, `/ask` | M2, M3 |
| API-Doku | keine | OpenAPI + Scalar UI | M2 |
| Schutz vor Halluzinationen | keiner | `MinScore` + Prompt-Regeln + Zitate | M3 |
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

Danach `/health/live` (200) und `/health/ready` aufrufen. Letzteres liefert 503, bis M2 das Indexieren bringt.

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

Alle Entscheidungen, auch die geplanten, stehen im [Decision Log in SPEC.md](SPEC.md#20-decision-log).
