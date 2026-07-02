# Architecture

## Overview

A citizen files an *ansökan* through the **e-tjänst** (Blazor portal). A case-officer agent (the *handläggaragent*, "Bo Sken") receives it, decides it against a small rulebook using Claude, executes approved actions through Home Assistant, and records everything in an append-only log (the *diariet*). The *beslut* is pushed back to the portal live.

```
        Citizen (sökande)
             │  fills in "ansökan" on the e-tjänst
             ▼
   ┌───────────────────────┐
   │  Lampverket.Web        │  Blazor Web App (e-tjänst portal, SignalR)
   │  (e-tjänst portal)     │
   └───────────┬────────────┘
               │ Ansokan
               ▼
   ┌───────────────────────┐
   │  Lampverket.Agent      │  HandlaggareService — bureaucratic gatekeeping
   │  (orchestrator)        │   1. assign diarienummer
   │                        │   2. enforce fikahelgd
   └───────────┬────────────┘   3. delegate to HandlaggareAgent
               │
               ▼
   ┌───────────────────────┐       agentic loop (multi-turn tool use)
   │  HandlaggareAgent      │   ┌──────────────────────────────────────────┐
   │  "Bo Sken"             │◄──┤ Claude ↔ HA MCP tools + lamna_beslut     │
   │  (Anthropic SDK + MCP) │   │  - Claude calls GetLiveContext, decides, │
   │                        │   │    issues lamna_beslut, then acts.       │
   └───────────┬────────────┘   │  - C# guards: beslut-before-action,      │
               │                │    max iterations, allowed entities.     │
               │                └──────────────────────────────────────────┘
               ▼                            ↑
   ┌───────────────────────┐                │ HassTurnOn / HassLightSet / …
   │  Diariet (audit log)   │       ┌───────┴────────┐
   └───────────────────────┘       │  HA MCP Server │
               │ beslut pushed back via SignalR
               ▼
        Ärende page shows the decision; the light changes
```

## Projects (the .NET solution)

| Project | Responsibility |
| --- | --- |
| **Lampverket.Web** | Blazor e-tjänst portal: forms, kvittens, Mina ärenden, the live ärende view. The "maximalt myndighetstrist" UI. See [`WEBAPP.md`](WEBAPP.md). |
| **Lampverket.Core** | Domain model and rules: `Ansokan`, `Arende`, `Beslut`, `Beslutstyp`, `Enhet`, the legal codex, the state machine, and the diariet contracts. No external dependencies. |
| **Lampverket.Agent** | Two layers: (1) `HandlaggareService` — case registration, diarienummer, fikahelgd gate, diariet writes. (2) `HandlaggareAgent` — the multi-turn agentic loop. Connects to Home Assistant's MCP Server directly via `BetaMcp.ListToolsAsync` / `McpClient`. Config and the readiness `HomeAssistantHealthCheck` live in the `HomeAssistant/` sub-folder of this project. |

## Components

### Handläggaragent ("Bo Sken")

Lives in `Lampverket.Agent`. The **system prompt** encodes:

1. The **persona** — a deadpan Swedish civil servant (see [`BUREAUCRACY.md`](BUREAUCRACY.md#tone-guide)).
2. The **legal codex** — the fictional statutes it cites.
3. The **beslut template** — the exact decision format it must emit via `lamna_beslut`.
4. The **state machine** — the lifecycle every ärende follows.
5. The **tool protocol** — call `GetLiveContext` first; call `lamna_beslut` before any action tool; allowed entity IDs.

#### Agentic loop (the design choice)

The agent runs a **multi-turn tool-use loop** rather than a single forced-tool-use call. Each turn:

1. Send the conversation + the merged tool list (HA MCP tools + `lamna_beslut`) to Claude.
2. If Claude emits one or more `tool_use` blocks, execute them (MCP for HA tools, decision capture for `lamna_beslut`) and feed the results back as the next user turn.
3. Repeat until Claude responds without a tool call, or a hard limit is hit.

This is the classic agent pattern (Anthropic's "Building Effective Agents"): the LLM controls the flow; the host provides tools, executes them, and feeds back results.

**Why this over a single forced-tool call?**

- The LLM, not C#, decides when to read state, when to issue the decision, and which action follows. That is what makes this an *agent* rather than a workflow.
- Real HA tool schemas (`GetLiveContext`, `HassTurnOn`, `HassLightSet`, …) are passed through unmodified — the demo shows MCP integration end-to-end.
- The cost of multi-turn (more tokens, latency) is bounded by `MaxIterations` and mitigated with prompt caching of the system prompt.

#### Hybrid guardrails (what stays in C#)

Agent autonomy is layered with deterministic guards. The agent loop enforces them before any HA tool reaches MCP:

| Layer | Owner | Enforcement |
| --- | --- | --- |
| Decision flow | Claude | Tool choices, ordering, when to stop |
| **Beslut before action** | C# guard (`HaToolFactory.GuardHaTool`) | If a HA action tool is called before `lamna_beslut`, return an error tool-result (a normal, non-`is_error` result) instructing Claude to issue a beslut first |
| **Max iterations** | C# guard | Hard cap on loop turns; log and bordlägg on exhaustion |
| Allowed entities | System prompt + config | Entity IDs listed in the prompt come from `HomeAssistantOptions.Devices` |
| Audit | `HandlaggareService` + diariet | Every ärende append-logged at intake and after the agent returns |
| Prompt cost | `cache_control: ephemeral` | The `lamna_beslut` tool definition is marked ephemeral today. Caching the system prompt and the HA tool list across loop turns is planned but not yet wired (tracked as an issue). |

**Division of labour:** Claude *decides and orchestrates*; C# *enforces invariants, executes side effects, and audits*. The HA MCP call is still a real side effect — it just travels through Claude's `tool_use`, not through a `HandlaggareService.VerkstallAsync` switch statement.

### Home Assistant integration (inside Lampverket.Agent)

- **Read:** current device/area state — via `GetLiveContext`.
- **Act:** turn on/off, set brightness, set volume, play media — via `HassTurnOn` / `HassTurnOff` / `HassLightSet` / `HassSetVolume` / `HassMediaSearchAndPlay`.
- Implemented with the official **C# MCP SDK** (`ModelContextProtocol`) plus the Anthropic SDK's `Anthropic.Mcp` helper. `HandlaggareAgent` holds an `McpClient` connected to Home Assistant's built-in **MCP Server**. At first ärende it calls `BetaMcp.ListToolsAsync(mcpClient)` once — the resulting `IBetaRunnableTool[]` is reused for every loop. Tool calls flow through `BetaToolRunner`, wrapped by a guard delegate (`HaToolFactory.GuardHaTool`) that enforces beslut-before-action. The `HomeAssistant/` sub-folder of the Agent project holds the bound `HomeAssistantOptions`, `DeviceMapEntry` configuration record, and `HomeAssistantHealthCheck`. See [`DEVICES.md`](DEVICES.md).

### Diariet (Lampverket.Core)

An append-only record of every ärende. JSONL file for the weekend; SQLite via EF Core as an upgrade. Suggested record:

```json
{
  "diarienummer": "LV-2026-001428",
  "mottaget": "2026-05-25T21:42:01+02:00",
  "sokande": "Simon",
  "ansokan": "Tända lampan i sovrummet, det är mörkt.",
  "berord_enhet": "light.bedroom_banan",
  "beslut": "bifall",
  "villkor": "ljusstyrka begränsad till 60 % (3 § lagom)",
  "motivering_kort": "Berättigat behov av belysning; lagom-justerad.",
  "lagrum": ["7 § lagen (2026:1) om skälig hemtrevnad", "3 §"],
  "verkstalld": "2026-05-25T21:42:07+02:00",
  "overklagad": false
}
```

The log is **append-only by rule** — corrections are issued as new ärenden (an *omprövning*), never by editing history. That mirrors a real diarium and keeps the audit-trail gag honest.

## The case state machine

| # | State | Swedish | What happens |
| - | --- | --- | --- |
| 1 | Received | *Inkommet* | Application logged, `diarienummer` assigned |
| 2 | Decided | *Beslutat* | bifall / delvis bifall / avslag / avvisning issued, with a `motivering`. A bifall stays here if execution fails |
| 3 | Executed | *Verkställt* | On bifall/delvis bifall, the HA action was confirmed (or was unneeded — device already in the desired state) |
| 4 | Tabled | *Bordlagt* | Unknown device, processing error, or no valid beslut |

"Under handläggning" is not a stored state — it is derived: an ärende with no `Beslut` yet is shown as under review. Decision types are specified in [`BUREAUCRACY.md`](BUREAUCRACY.md#decision-types).

## Request flow (one ärende)

1. **Intake.** Citizen submits the form; `Lampverket.Web` builds an `Ansokan` and calls `HandlaggareService`.
2. **Register.** Assign `diarienummer`; write the `Inkommet` record; redirect the user to the kvittens.
3. **Fikahelgd gate.** If it is Friday 14:xx in Stockholm, short-circuit with an auto-avslag — `HandlaggareAgent` is not invoked.
4. **Agent loop.** `HandlaggareAgent.HandlaggaAsync(arende)` runs the multi-turn loop:
   - Claude inspects state via `GetLiveContext`.
   - Claude issues a `lamna_beslut` (capturing the structured decision into a `Beslut`).
   - On bifall/delvis bifall, Claude calls the appropriate HA action tool; on avslag/avvisning, the loop ends.
   - The C# guard rejects any HA action emitted before `lamna_beslut`.
5. **Log — two phases.** Append the *beslutsfas* record (`Beslutat`/`Bordlagt`), then, if the beslut allows execution, append the *verkställighetsfas* record whose status C# reconciles from the observed HA outcome (`Verkstallighetsregler`): `Verkställt` on a confirmed (or unneeded) action, back to `Beslutat` if the action failed. `Verkställt` is never inferred from the decision type alone.
6. **Notify.** An in-process `IArendeNotifier` pushes the finished ärende to the open page over its existing Blazor circuit; the light changes in the room.

## Tech stack and rationale

| Layer | Choice | Why |
| --- | --- | --- |
| Front end | Blazor Web App (Server) | C# end-to-end; SignalR for live beslut push; the trist UI is plain CSS |
| Agent | C# + `Anthropic.SDK` | Calls Claude from .NET; tool-use for structured decisions |
| Decision shape | Claude tool-use → `Beslut` | Schema-validated output, deserialized and checked in C# |
| Device control | C# MCP SDK → HA MCP Server | One MCP client calling Home Assistant's Assist tools |
| Persistence | JSONL → SQLite/EF Core | No infra for the weekend; the log *is* the audit trail |
| Secrets | user-secrets / env vars | Keys and tokens out of git |

## Error handling

- **Unavailable device** → Claude sees the failure tool-result and issues a *bordläggs*-style beslut; logged, not crashed.
- **Ambiguous request** → *avvisning* asking the applicant to complete the application.
- **Claude calls a HA action before `lamna_beslut`** → the C# guard (`HaToolFactory.GuardHaTool`) returns an error tool-result instructing Claude to issue a beslut first; the call never reaches HA. There is no dedicated violation counter — the only bound on repeated attempts is `MaxIterations`.
- **Claude returns an invalid/wrong-shaped beslut** → JSON deserialisation logs a warning; loop continues so Claude can correct on the next turn. If no valid beslut by `MaxIterations`, *bordlägg* with a *handläggningsfel* note.
- **MaxIterations reached** → loop exits with a *handläggningsfel* bordläggning; the unfinished trail is preserved in the diariet for debugging.
- **Home Assistant call fails** → the MCP `is_error` reaches Claude as a tool result; Claude amends the beslut's `verkstallighet` field accordingly; the diariet records the final state.
