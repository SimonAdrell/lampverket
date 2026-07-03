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
- **Agent:** a C# *handläggare* service calling Claude via the official **`Anthropic`** NuGet (with `Anthropic.Mcp` for MCP integration). Runs a multi-turn agentic loop via the SDK's `BetaToolRunner`; Claude orchestrates HA MCP tool calls and the structured `lamna_beslut` tool.
- **Device control:** Home Assistant via the official **C# MCP SDK** (`ModelContextProtocol`). `Lampverket.Agent` connects directly to Home Assistant's built-in **MCP Server** and exposes its Assist tools (`GetLiveContext`, `HassTurnOn`, `HassTurnOff`, `HassLightSet`, `HassSetVolume`, `HassMediaSearchAndPlay`) to Claude. See `docs/DEVICES.md`.
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
    ├── Lampverket.AppHost/       # .NET Aspire AppHost — service orchestration
    ├── Lampverket.ServiceDefaults/ # .NET Aspire shared defaults
    ├── Lampverket.ApiService/    # Aspire API service (default template)
    ├── Lampverket.Web/           # Blazor e-tjänst portal
    ├── Lampverket.Web.Tests/     # bUnit tests for the portal
    ├── Lampverket.Core/          # domain: Ansokan, Arende, Beslut, codex, state machine, diariet
    ├── Lampverket.Agent/         # handläggaragent: Anthropic SDK loop + HA MCP integration
    │   └── HomeAssistant/        # HA config, HealthCheck, device map (folded in from former Lampverket.HomeAssistant project)
    └── Lampverket.Agent.Tests/   # xUnit tests for the agent + handläggarservice
```

## Conventions and rules

These are load-bearing for the project to "work" as intended:

1. **Stay in character.** The handläggare ("Bo Sken") is a deadpan Swedish civil servant. The comedy comes from competence and formality, never from breaking character.
2. **Swedish domain terms are intentional — keep them.** In prose *and* in code: domain types use Swedish names (`Ansokan`, `Arende`, `Beslut`, `Diarienummer`, `Beslutstyp`). Drop diacritics in identifiers (`Arende`), keep å/ä/ö in user-facing strings. See `docs/BUREAUCRACY.md` for the glossary.
3. **No action without a decision.** Claude must call `lamna_beslut` *before* any Home Assistant action tool. Enforced by a C# guard in `HaToolFactory.GuardHaTool` that returns an error tool-result (a normal, non-`is_error` result) for any HA action tool emitted before the beslut.
4. **Everything is logged.** Every ärende (received, decided, executed, appealed) is appended to the diariet with its diarienummer. The audit trail is a feature.
5. **Respect the state machine.** Inkommet → Beslutat → Verkställt (bifall/delvis bifall, execution confirmed; stays Beslutat if it fails), or Inkommet → Beslutat (avslag/avvisning), or Inkommet → Bordlagt. "Under handläggning" is derived (no `Beslut` yet), not a stored status. See `docs/ARCHITECTURE.md`.
6. **Check before acting.** Claude must call `GetLiveContext` for the affected entity before issuing the beslut (instructed via the system prompt).
7. **Fail in character.** An unavailable device or MCP error is surfaced to Claude as a tool result; Claude responds with a *bordläggs*-style beslut, not a stack trace.
8. **Agentic loop with deterministic guardrails.** Claude *orchestrates*; C# *enforces invariants* (beslut-before-action, max iterations, allowed entity scope). The HA call is still a real side effect — it travels through Claude's `tool_use`, not a switch statement in `HandlaggareService`.

## Safety

- Never commit secrets. Use user-secrets / environment variables; `.gitignore` excludes local secret files.
- The agent acts on a real home. Keep destructive scope narrow: lights, speakers, and explicitly listed entities only. No locks, alarms, or anything safety-critical.

## Code style

- **Always use braces on `if` statements**, even single-line bodies. No braceless `if`.

## Where to start

Read `docs/CONTEXT.md`, then `docs/ARCHITECTURE.md`, `docs/BUREAUCRACY.md`, and `docs/WEBAPP.md`. Build order is in `docs/ROADMAP.md`.

