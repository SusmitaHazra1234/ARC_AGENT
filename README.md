# ARC — Agentic Receivables Recovery and Section 138 Legal Action Pipeline

ARC helps PaintCo recover overdue dealer receivables and progress Section 138 legal cases through governed Microsoft Agent Framework (MAF) workflows. Agents recommend; tools and domain rules own amounts, dates, and eligibility; humans approve at gates.

The current implementation defaults to **Shadow mode**: local demonstration and testing without real outbound notice, courier, court filing, or production legal submission.

| Source | Role |
|--------|------|
| `docs/Problem_Statement_1_Receivables_Recovery_Section138 (1).pdf` | Business requirements |
| `docs/ARC_Business_Requirements_Document.html` | BRD |
| This repository | Implementation |

---

## What is ARC?

PaintCo has a large dealer network. When receivables go overdue, operations today rely on ODOS demand notices and, after bounce + non-payment, Section 138 legal escalation. ARC automates the **recommendation and orchestration** of that work: reconcile AR, prioritise dealers, recommend notice or reconcile, check legal eligibility and limitation clocks, verify drafts, plan field visits, and assemble evidence — always through tools, rules, and human gates.

Two workflows are implemented: **`OdosCycle`** (monthly demand-notice cycle) and **`Section138`** (legal pipeline). Court filing itself is out of scope in the current code.

---

## High-level architecture

```
                         ARC
                          |
              +-----------+-----------+
              |                       |
           ARC.Api                 ARC.Cli
         (HTTP API)          (local Shadow demo)
              |                       |
              +-----------+-----------+
                          |
              (+ ARC.Host.Functions for Azure-hosted runs)
                          |
                    MAF Workflow
                 OdosCycle / Section138
                          |
                       A1–A8
                       Agents
                          |
                      ARC.Tools
                    /            \
                   /              \
          ARC.Knowledge          ARC.Data
                   \              /
                    \            /
                       ARC.Domain
```

WhatsApp is **not currently implemented**. There is **no UI** in this repository.

---

## Important architecture principle

```
Agent
  ↓
Approved Tool
  ↓
Domain / Data / Knowledge
  ↓
Result
  ↓
Agent (explains; does not override numbers)
```

**Verified:** A1–A8 agent classes call approved tools for business results (exposure, tier, notice verdict, eligibility, draft check, field/PTP, case file, insights). Agents must not approve human gates.

**Also verified:** MAF workflow executors in `ARC.Agents` use data repositories and messaging for load/persist, gate audit, and notifications — not only tools. Tools remain the controlled path for **decision amounts and eligibility**.

---

## Project structure

```
src/
├── ARC.Domain           # Rules, entities, limitation clock, recovery state
├── ARC.Data             # SQL, Cosmos, Blob, Service Bus
├── ARC.Knowledge        # Document Intelligence, Cosmos dense + Lucene lexical hybrid retrieval, graph
├── ARC.Tools            # Deterministic tools used by agents
├── ARC.Agents           # A1–A8, MAF workflows, Shadow outbound gate
├── ARC.Host.Functions   # Azure Functions host (timer / queues / resume)
├── ARC.Api              # REST API
└── ARC.Cli              # Local S1–S9 Shadow runner (no Azure)

tests/
├── ARC.Domain.Tests     # Domain and gate unit tests
├── ARC.Agents.Tests     # Agent / workflow / Shadow tests
└── ARC.Eval             # Golden-set acceptance (BRD §22)
```

| Project | Responsibility |
|---------|----------------|
| ARC.Domain | Business rules and domain model |
| ARC.Data | Persistence and messaging adapters |
| ARC.Knowledge | Document Intelligence; Cosmos dense + Lucene BM25 hybrid search |
| ARC.Tools | Authoritative tool operations |
| ARC.Agents | Agents + MAF workflows |
| ARC.Host.Functions | Hosted cycle and gate resume |
| ARC.Api | HTTP surface for ops and gates |
| ARC.Cli | Local Shadow demo |
| ARC.Domain.Tests | Domain tests |
| ARC.Agents.Tests | Agent/workflow tests |
| ARC.Eval | Acceptance evaluation |
| ARC.Integration.Tests | Durable resume tests (Cosmos Emulator + SQL). See `tests/ARC.Integration.Tests/README.md`. |

**Target framework:** .NET 8 (`net8.0`).

---

## Agents

| Agent | Responsibility |
|-------|----------------|
| A1 Reconciliation | Coordinates reconciliation; amounts from `ComputeNetExposure` |
| A2 Risk Prioritisation | Tier/score from `PrioritiseRecovery` |
| A3 Notice Decisioning | Issue / Hold / Reconcile from `DecideNotice` (no despatch) |
| A4 Legal Eligibility | Eligibility + limitation clock from legal tools |
| A5 Drafting Verification | Draft check via `VerifyDraft` |
| A6 Field Orchestration | Visits / PTP structure via `OrchestrateField` (never confirms PTP) |
| A7 Evidence Case File | Completeness via `PrepareCaseFile` |
| A8 Supervisory Insight | Exception queue via `GetSupervisoryInsights` |

**ODOS graph:** A1 → A2 → A3 or A6 → (G1) → A5 → (G2) → A6  
**Section 138 graph:** A1 → A4 → (G3) → A5 → (G2) → A7 → (G4)  
**A8** is implemented and used from the API insights endpoints; it is **not** on the ODOS/S138 graphs.

---

## Tools

| Tool | Responsibility |
|------|----------------|
| `ComputeNetExposure` | Net recoverable exposure |
| `PrioritiseRecovery` | Recovery tier and score |
| `DecideNotice` | Issue / Hold / Reconcile |
| `SearchDocuments` / `TraverseGraph` | Knowledge citations |
| `CheckSection138Eligibility` | Eligibility + clock / alerts |
| `VerifyDraft` | Draft field verification |
| `OrchestrateField` | Visit planning, PTP structure |
| `PrepareCaseFile` | Case-file completeness |
| `GetSupervisoryInsights` | Supervisory exception queue |

There is **no** `SubmitNotice` tool.

---

## Workflow

### ODOS (`OdosCycle`)

```
Start
  ↓
A1 Reconciliation
  ↓
A2 Risk / Prioritisation
  ↓
A3 Notice decision  ──or──  A6 Visit (if Visit tier)
  ↓
G1 Depot Manager (if Issue)
  ↓
A5 Drafting / Verification
  ↓
G2 Advocate
  ↓
A6 Field (visit plan)
  ↓
Shadow outcome (outbound suppressed)
```

### Section 138 (`Section138`)

```
Start
  ↓
A1 Reconciliation
  ↓
A4 Legal eligibility + clock
  ↓
G3 Legal progression
  ↓
A5 Drafting / Verification
  ↓
G2 Advocate
  ↓
A7 Evidence case file
  ↓
G4 Legal case-file review
  ↓
Complete (court filing out of scope)
```

Checkpoint / resume is used when a human gate waits (CLI S8 demonstrates this in-memory).

---

## Human-in-the-loop

```
Workflow
   ↓
Approval required
   ↓
WAIT
   ↓
Human decision
   ↓
Persist decision
   ↓
Resume workflow
```

| Gate | Approver | Where |
|------|----------|-------|
| G1 Depot Manager | Depot Manager | ODOS after Issue |
| G2 Advocate signature | Advocate | ODOS and Section 138 |
| G3 Legal progression | Legal | Section 138 after eligibility |
| G4 Legal case-file review | Legal | Section 138 after evidence |

- Approval requires an **explicit** human decision.  
- **Timeout / expiry is not approval** (`gate_expired`).  
- **Rejection is not approval.**  
- Missing response leaves the workflow waiting.

---

## Shadow Mode

Shadow is the **current default** execution mode for demonstration and testing.

| Fact | Verified by |
|------|-------------|
| CLI runs `RunMode.Shadow` | `ARC.Cli` scenarios |
| API default `ArcApi:DefaultRunMode` = Shadow | `appsettings.json` |
| Host default `ArcHost:DefaultRunMode` = Shadow | `local.settings.json` |
| Outbound = log / suppress only | `ShadowOutboundGate` / CLI recorder |
| CLI banner | *“No Azure, no Live outbound.”* |

No real notice despatch, courier despatch, court filing, or production external legal submission is performed by the local Shadow demo. `Assisted` / `Live` enum values exist; **Live outbound is not registered**.

---

## S1–S9 demo

| Scenario | Status | What it demonstrates |
|----------|--------|----------------------|
| S1 | PASS | Clean overdue → Issue → G1/G2 → visit (Shadow suppressed) |
| S2 | PASS | High credit-note ratio → R1b Reconcile → Finance (no gate) |
| S3 | PASS | Qualifying bounce → Section 138 case file (court filing out of scope) |
| S4 | PASS | Non-qualifying bounce → R2 block before courier |
| S5 | PASS | T-2 limitation alert to covering TSI |
| S6 | PASS | Moratorium → R5 block at A1 |
| S7 | PASS | Voice PTP 0.72 → TSI confirmation required |
| S8 | PASS | Halt at G1, resume from checkpoint |
| S9 | PASS | SAP + portal lines on one canonical dealer URN |

---

## Build

```bash
dotnet restore ARC.sln
dotnet build ARC.sln
```

Stop a running `ARC.Api` if you see DLL file-lock errors.

---

## Test

```bash
dotnet test ARC.sln --filter FullyQualifiedName!~ARC.Integration.Tests
dotnet test tests/ARC.Integration.Tests/ARC.Integration.Tests.csproj
```

Unit/agent tests do not require Cosmos. Integration tests (`ARC.Integration.Tests`) require Cosmos Emulator (or `ARC_COSMOS_CONNECTION_STRING`) and LocalDB (or `ARC_SQL_CONNECTION_STRING`). They fail with `BLOCKED BY ENVIRONMENT` when that infrastructure is missing — they do not fall back to in-memory MAF checkpoints.

---

## CI

GitHub Actions workflow: `.github/workflows/ci.yml`

On push to `main`/`master`, pull requests, and manual `workflow_dispatch`, CI runs:

1. `dotnet restore ARC.sln`
2. `dotnet build ARC.sln` (Release)
3. `dotnet test ARC.sln --filter FullyQualifiedName!~ARC.Integration.Tests` (unit/agent tests; integration tests need Cosmos Emulator)
4. `dotnet run --project src/ARC.Cli -- all` (Shadow S1–S9)

No Azure secrets are required. Live outbound is not enabled.

---

## Run Shadow demo

```bash
dotnet run --project src/ARC.Cli -- all
```

Runs local S1–S9. **No Azure. No Live outbound.**  
Single scenario: `dotnet run --project src/ARC.Cli -- S1`.

In Visual Studio: set **ARC.Cli** as the startup project (not `ARC.Domain` or other libraries).

---

## API (Development)

```bash
dotnet run --project src/ARC.Api --launch-profile http
```

Default URL: `http://localhost:5187`

| Method | Path | Purpose |
|--------|------|---------|
| GET | `/health` | Liveness (no Azure) |
| GET | `/v1/cycles/{cycleId}/dashboard` | Cycle dashboard |
| GET | `/v1/cycles/{cycleId}/cases` | Case list |
| GET | `/v1/cycles/{cycleId}/dealers/{dealerUrn}` | Case detail |
| POST | `/v1/gates/{gateId}/decisions` | Human gate decision (queues resume) |
| POST | `/v1/cycles/{cycleId}/dealers/{dealerUrn}/runs` | Start run (queues fan-out) |
| GET | `/v1/insights/exceptions` | A8 exceptions |
| POST | `/v1/insights/nlq` | A8 NLQ |

Dashboard, gates, runs, and insights need Azure when data/messaging clients are resolved. `/health` does not.

### Development headers (JWT not configured)

| Header | Purpose |
|--------|---------|
| `X-Arc-Upn` | Actor UPN |
| `X-Arc-Role` | Role (`DepotManager`, `Advocate`, `Legal`, `Tsi`, `DepotAdmin`, `Finance`) |
| `X-Arc-Region` | Required for TSI |
| `X-Arc-Depot` | Required for Depot Manager |

---

## Host / Azure

| Area | Local Shadow (`ARC.Cli`) | Hosted (`ARC.Api` / `ARC.Host.Functions`) |
|------|--------------------------|------------------------------------------|
| Azure | Not required | Required beyond `/health` |
| SQL | In-memory fakes | `ArcData:Sql` |
| Cosmos | In-memory fakes | `ArcData:Cosmos` |
| Blob | Not used by CLI demo | `ArcData:Blob` |
| Service Bus | In-memory recorder | `ArcData:ServiceBus` |
| Document Intelligence | Not used by CLI demo | `ArcKnowledge:DocumentIntelligenceEndpoint` |
| Document search | Not used by CLI demo | Cosmos dense (`embedding` + VectorDistance) + Lucene BM25 (`ArcKnowledge:LexicalIndexDirectory`), fused with RRF |
| Outbound | Shadow suppressed | Shadow gate registered; Live not enabled |

Host triggers: monthly ODOS timer, cycle fan-out queue, gate resume (Service Bus + HTTP `gates/resume`).

---

## Configuration

| File | Used by |
|------|---------|
| `src/ARC.Api/appsettings.json` | API |
| `src/ARC.Api/appsettings.Development.json` | Dev logging |
| `src/ARC.Host.Functions/local.settings.json` | Functions local |
| `src/ARC.Cli/Properties/launchSettings.json` | CLI debug profiles |
| Options: `ArcApiOptions`, `ArcHostOptions`, `ArcDataOptions`, `ArcKnowledgeOptions`, `ArcToolsOptions` | Typed config |

Never commit secret values. The API and Functions host take only the vault URL, then load every secret from that vault:

```json
"KeyVault": {
  "VaultUri": "https://mcc-arc-key-vault.vault.azure.net/"
}
```

Use `az login` or a Visual Studio Azure account with **Key Vault Secrets User** on the vault. Nested config keys in the vault use `--` (for example `ArcData--Sql--ConnectionString`).

---

## Legal / Finance — TBC

These items require confirmation from the appropriate Legal, Finance, or Business stakeholders before being treated as final rules. Do not invent values in code or prompts.

| Item |
|------|
| NI Act notice / cure / filing windows (illustrative 30/15/30) and clock anchor |
| R1b denominator (claim amount vs gross open AR) |
| Visit vs Notice cutoffs |
| ASR discard floor |
| Process B 60-day trigger vs illustrative filing clock |

Also listed in BRD §23.1 / Architecture §23: second-bounce anchor, holidays, R1c PTP grace, A2 tier boundaries, Hold vs Reconcile routing, 15th timeline freeze, arrest-warrant automation, physical courier chain.

---

## Demonstration flow

```
User / Input
   ↓
ARC (Api or Cli)
   ↓
MAF Workflow
   ↓
Agents
   ↓
Tools
   ↓
Domain / Data / Knowledge
   ↓
Decision
   ↓
Human Gate where required
   ↓
Shadow Result
```

---

## Current status

| Area | Status |
|------|--------|
| Domain | Implemented |
| Data | Implemented |
| Knowledge | Implemented |
| Tools | Implemented |
| Agents | Implemented |
| Host | Implemented |
| API | Implemented |
| CLI | Implemented |
| Evaluation | Implemented |
| Unit / agent tests | Passing (current validation: 41/41) |
| Integration tests project | Implemented (`tests/ARC.Integration.Tests`, AC#2 cold resume) |
| Shadow demo S1–S9 | Passing |
| Live outbound | Disabled |
| WhatsApp | Not currently implemented |
| UI | Not currently implemented |
| CI | Implemented (`.github/workflows/ci.yml`) |

---

## Current out of scope

- Live / Assisted outbound despatch (not registered)
- Court filing automation
- Physical courier automation
- WhatsApp / production messaging channels
- Production UI
- Identity-merge engine (S9 uses a pre-resolved canonical URN)
- Arrest-warrant escalation agent

---

## Future work

*Planned — not implemented*

1. Validate / finalize Legal and Finance TBC items  
2. ~~CI~~ (done — see `.github/workflows/ci.yml`)  
3. Azure hosted Shadow cycle (dev/test)  
4. WhatsApp integration if required  
5. UI if required  
6. UAT  
7. Production / Live approval  

---

## BRD / Implementation gaps

| Topic | Gap |
|-------|-----|
| Integration tests | BRD/structure discussions may assume them; **project not in solution** |
| A8 on recovery graphs | A8 exists via API insights; **not** on ODOS/S138 MAF graphs |
| BRD S5 “3 days left” | Domain T-2 fires when remaining **= 2** (CLI documents this) |
| Visit tier | `VisitMaxNetExposure` unset → Visit not auto-assigned |
| Assisted / Live modes | Enum exists; **only Shadow outbound gate** is registered |
| Workflow I/O | Executors use repositories/messaging directly for orchestration, not tools alone |

---

## Safety note

> **IMPORTANT:** Shadow mode is the current default. No real outbound notice, courier, court filing, or other live external action is performed by the local Shadow demonstration.
