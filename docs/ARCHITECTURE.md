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
   │  Lampverket.Agent      │  HandläggareService
   │  "Bo Sken"             │   1. assign diarienummer
   │  (Anthropic.SDK)       │   2. read state ──────────►  Home Assistant MCP Server (GetLiveContext)
   └───────────┬────────────┘   3. ask Claude for a beslut (tool-use, structured)
               │                 4. on bifall: verkställ ─►  Home Assistant MCP Server (HassTurnOn / HassLightSet …)
               ▼
   ┌───────────────────────┐
   │  Diariet (audit log)   │  append-only (JSONL → SQLite)
   └───────────────────────┘
               │ beslut pushed back via SignalR
               ▼
        Ärende page shows the decision; the light changes
```

## Projects (the .NET solution)

| Project | Responsibility |
| --- | --- |
| **Lampverket.Web** | Blazor e-tjänst portal: forms, kvittens, Mina ärenden, the live ärende view. The "maximalt myndighetstrist" UI. See [`WEBAPP.md`](WEBAPP.md). |
| **Lampverket.Core** | Domain model and rules: `Ansokan`, `Arende`, `Beslut`, `Beslutstyp`, `Enhet`, the legal codex, the state machine, and the `DiariumService`. No external dependencies. |
| **Lampverket.Agent** | The `HandlaggareService` — orchestrates a case: registers it, reads state, calls Claude via `Anthropic.SDK` with the Bo Sken system prompt + tool definitions, validates the structured `beslut`. |
| **Lampverket.HomeAssistant** | `IHomeAssistantClient` — an MCP client (C# MCP SDK) to Home Assistant's MCP Server; reads state and performs actions through its Assist tools. |

## Components

### Handläggaragent ("Bo Sken")

Lives in `Lampverket.Agent`. The **system prompt** encodes four things:

1. The **persona** — a deadpan Swedish civil servant (see [`BUREAUCRACY.md`](BUREAUCRACY.md#tone-guide)).
2. The **legal codex** — the fictional statutes it cites.
3. The **beslut template** — the exact decision format it must emit.
4. The **state machine** — the lifecycle every ärende follows.

Claude returns a **structured beslut via tool-use** (a `lamna_beslut` tool whose schema mirrors the `Beslut` type), which C# deserializes and validates. One agent is enough for the weekend build; an optional later split adds a *registrator* (intake + diarienummer) handing off to a *handläggare* (decision), mirroring a real authority.

**Division of labour:** Claude *decides*; C# *executes*. Claude never calls Home Assistant directly — it returns a decision, and `HandlaggareService` performs the action only on *bifall*/*delvis bifall*. This keeps the side effects in deterministic, testable C#.

### Home Assistant (Lampverket.HomeAssistant)

- **Read:** current device/area state, to drive the personality rules (already-on, brightness, availability) — via `GetLiveContext`.
- **Act:** turn on/off, set brightness, set volume, play media — via `HassTurnOn` / `HassTurnOff` / `HassLightSet` / `HassSetVolume` / `HassMediaSearchAndPlay`.
- Implemented with the official **C# MCP SDK** (`ModelContextProtocol`): `Lampverket.HomeAssistant` is an MCP *client* that connects to Home Assistant's built-in **MCP Server** integration and calls its Assist tools. The decision is Claude's; this client only carries out an already-issued *beslut*. See [`DEVICES.md`](DEVICES.md).

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
| 2 | Under review | *Under handläggning* | Agent reads current device state |
| 3 | Decision | *Beslut* | bifall / delvis bifall / avslag / avvisning, with a `motivering` |
| 4 | Executed | *Verkställt* | On bifall, the action is performed via Home Assistant |
| 5 | (optional) Appeal | *Överklagande* | The applicant contests; the agent re-decides |

Decision types are specified in [`BUREAUCRACY.md`](BUREAUCRACY.md#decision-types).

## Request flow (one ärende)

1. **Intake.** Citizen submits the form; `Lampverket.Web` builds an `Ansokan` and calls `HandlaggareService`.
2. **Register.** Assign `diarienummer`; write the `Inkommet` record; redirect the user to the kvittens.
3. **Review.** Read the affected device/area state; evaluate codex + personality rules (fikahelgd, lagom caps, neighbour-volume, already-on).
4. **Decide.** Call Claude; receive a structured `beslut`; validate it against `Lampverket.Core`.
5. **Execute.** On bifall/delvis bifall, call Home Assistant. If unavailable, *bordlägg* in character.
6. **Log.** Append the final record (with verkställighet timestamp) to the diariet.
7. **Notify.** Push the `beslut` to the ärende page via SignalR; the light changes in the room.

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

- **Unavailable device** → `bordläggs` in character; logged, not crashed.
- **Ambiguous request** → *avvisning* asking the applicant to complete the application.
- **Claude returns an invalid/owrong-shaped beslut** → validation fails in C#; retry once with the schema reinforced, then *bordlägg* with a *handläggningsfel* note.
- **Home Assistant call fails** → the beslut stands; the diariet records `verkstalld: false` with a *verkställighetshinder*; the page reports it deadpan.
