# CLAUDE.md — Lampverket

Operational context for Claude Code (and any AI agent) working in this repository.

## What this project is

**Lampverket — Myndigheten för Hemautomation** ("The Agency for Home Automation"). A reference/portfolio project that turns a home into a satirical Swedish government authority. A citizen files an *ansökan* (application) through an **e-tjänst** (web portal). A case-officer agent assigns a *diarienummer*, reviews the matter, issues a formal *beslut* (decision) with a *motivering*, then executes it through Home Assistant.

Under the joke it is a genuine demonstration of agentic **case handling** in a full C#/.NET stack: intake → review → structured decision → tool execution → audit log.

## Status

Concept and docs complete, implementation pending. See `docs/ROADMAP.md` for the build plan. Weekend-scoped project by Code by Simon (Apache 2.0).

## Tech stack

All-.NET, to showcase agentic AI in C#:

- **Runtime:** .NET 10, C#
- **Orchestration:** **.NET Aspire** (AppHost, ServiceDefaults, ApiService) — service composition and dev-time orchestration.
- **Front end:** **Blazor Web App** (interactive Server render mode) — the e-tjänst portal. SignalR pushes the *beslut* to the page live. See `docs/WEBAPP.md`.
- **Agent:** a C# *handläggare* service calling Claude via the **`Anthropic.SDK`** NuGet (there is no Anthropic-authored .NET SDK; this community SDK, or raw HTTP, is the standard route). Uses Claude tool-use for a structured `beslut`.
- **Device control:** Home Assistant via the official **C# MCP SDK** (`ModelContextProtocol`). `Lampverket.HomeAssistant` is an MCP *client* that connects to Home Assistant's built-in **MCP Server** and calls its Assist tools (`GetLiveContext`, `HassTurnOn`, `HassTurnOff`, `HassLightSet`, `HassSetVolume`, `HassMediaSearchAndPlay`). See `docs/DEVICES.md`.
- **Persistence (diariet):** append-only log — JSONL file for the weekend build, SQLite via EF Core as an upgrade.
- **Secrets:** Anthropic key + Home Assistant token via **.NET user-secrets** (dev) / environment variables (prod). Never in committed `appsettings.json`.

## Repo layout

```
lampverket/
├── CLAUDE.md                     # this file
├── .gitignore
├── README.md                     # (to add) public-facing intro
├── Lampverket.slnx               # solution file
├── docs/
│   ├── CONTEXT.md
│   ├── ARCHITECTURE.md
│   ├── BUREAUCRACY.md            # domain spec: codex, decision types, beslut template, persona
│   ├── DEVICES.md
│   ├── WEBAPP.md                 # the Blazor e-tjänst spec + design system
│   └── ROADMAP.md
└── src/
    ├── Lampverket.AppHost/       # .NET Aspire AppHost — service orchestration (built)
    ├── Lampverket.ServiceDefaults/ # .NET Aspire shared defaults (built)
    ├── Lampverket.ApiService/    # Aspire API service (built, default template)
    ├── Lampverket.Web/           # Blazor e-tjänst portal (scaffolded, not yet themed)
    ├── Lampverket.HomeAssistant/ # MCP client to Home Assistant's MCP Server (built)
    ├── Lampverket.HomeAssistant.Tests/ # unit tests for HomeAssistant (built)
    ├── Lampverket.HomeAssistant.TryIt/ # manual test harness for HomeAssistant (built)
    ├── Lampverket.Core/          # domain: Ansokan, Arende, Beslut, Enhet, codex, state machine, diariet (not yet created)
    └── Lampverket.Agent/         # handläggaragent: Anthropic.SDK + Claude tool-use (not yet created)
```

## Conventions and rules

These are load-bearing for the project to "work" as intended:

1. **Stay in character.** The handläggare ("Bo Sken") is a deadpan Swedish civil servant. The comedy comes from competence and formality, never from breaking character.
2. **Swedish domain terms are intentional — keep them.** In prose *and* in code: domain types use Swedish names (`Ansokan`, `Arende`, `Beslut`, `Diarienummer`, `Beslutstyp`). Drop diacritics in identifiers (`Arende`), keep å/ä/ö in user-facing strings. See `docs/BUREAUCRACY.md` for the glossary.
3. **No action without a decision.** The agent must issue a `beslut` *before* any Home Assistant call. Executing first and explaining later breaks the model.
4. **Everything is logged.** Every ärende (received, decided, executed, appealed) is appended to the diariet with its diarienummer. The audit trail is a feature.
5. **Respect the state machine.** Inkommet → Under handläggning → Beslut → Verkställt (or Avslag/Avvisning), with an optional Överklagande branch. See `docs/ARCHITECTURE.md`.
6. **Check before acting.** Read current device state before deciding (a light already on yields an *avslag* for an obehövligt ärende).
7. **Fail in character.** An unavailable device is logged as *"enheten är ur funktion; ärendet bordläggs"*, not as an error dump.
8. **The intelligence is Claude; the execution is C#.** Claude decides (structured `beslut` via tool-use); C# validates the decision and performs the Home Assistant action. Keep that separation clean.

## Safety

- Never commit secrets. Use user-secrets / environment variables; `.gitignore` excludes local secret files.
- The agent acts on a real home. Keep destructive scope narrow: lights, speakers, and explicitly listed entities only. No locks, alarms, or anything safety-critical.

## Where to start

Read `docs/CONTEXT.md`, then `docs/ARCHITECTURE.md`, `docs/BUREAUCRACY.md`, and `docs/WEBAPP.md`. Build order is in `docs/ROADMAP.md`.
