# Roadmap — build plan, demo, stretch

The e-tjänst is a **core component**, not a stretch. The build order below gets a working end-to-end portal in a weekend.

## Build tiers

- **Tier 1 (core, demoable):** A Blazor app with the application form → `HandlaggareService` (Claude decides) → Home Assistant (light changes) → `beslut` shown on the ärende page, with the diariet logging every case. Minimal styling.
- **Tier 2 (the polish that sells it):** The full "maximalt myndighetstrist" design system, the kvittens + Mina ärenden pages, the BankID gag, the öppettider/fika banner, and live SignalR push of the beslut.

## Weekend plan (~8 hours)

| Block | Time | Status | Output |
| --- | --- | --- | --- |
| A | 1.0 h | ✅ Done | Solution skeleton: `Lampverket.Web/.Core/.Agent`; domain types (`Ansokan`, `Arende`, `Beslut`, `Beslutstyp`, `Enhet`); device map in `appsettings.json` |
| B | 1.0 h | ✅ Done (PR #1) | HA MCP client wired through `Lampverket.Agent` — read state + call Assist tools. Confirm C# can turn on the hero light at a set brightness. (Originally `Lampverket.HomeAssistant`; folded into the Agent project once the agentic loop replaced the typed wrapper.) |
| C | 1.5 h | ✅ Done | `Lampverket.Agent`: Bo Sken system prompt + multi-turn agentic loop via `BetaToolRunner` returning a structured `beslut` via `lamna_beslut` (see `BUREAUCRACY.md`) |
| D | 1.0 h | 🔲 Blocked on C | Wire the form → agent → HA → diariet end to end (Tier 1 done); also resolves ROADMAP known issues C3a/C3b/C4 |
| E | 1.5 h | ✅ Done (PR #2) | `Lampverket.Web` UI: the maximalt-trist design system, ny-ansökan form, kvittens with diarienummer |
| F | 0.5 h | 🔶 Partial | Edge cases & personality: fikahelgd ✅, already-on avslag / lagom cap / Jante-klausulen need agent |
| G | 0.5 h | 🔶 Partial | Mina ärenden page ✅; live beslut push (SignalR) pending |
| H | 1.0 h | 🔲 Last | Record the demo (screen + a phone shot of the light); README (English + Swedish hook); publish |

Scope discipline: MCP from the start, JSONL before SQLite, one agent. BankID gag and the remiss/verksamhetsberättelse ideas are post-weekend.

## MVP checklist

- [x] Solution builds; projects wired (`Web`, `Core`, `Agent`, `Agent.Tests`, `Web.Tests`, Aspire trio)
- [x] HA MCP integration (inside `Lampverket.Agent`) reads state and can turn on the hero light from C#
- [x] Bo Sken system prompt complete and in character
- [x] Claude returns a schema-valid `beslut` via tool-use; C# validates it
- [x] Form on `/ny-ansokan` produces an `Ansokan` and a `diarienummer`
- [ ] On bifall the light actually changes
- [ ] Every ärende appended to the diariet
- [ ] At least three decision types shown (bifall, delvis bifall, avslag)
- [x] Fikahelgd rule working
- [ ] Graceful bordläggning for an unavailable device
- [x] Kvittens + Mina ärenden pages render with the trist design

## Demo script (~90 seconds)

1. **The portal.** Open the e-tjänst — sober blue gov site, öppettider banner, "Logga in med BankID". Log in (gag).
2. **The application.** `/ny-ansokan`: Ärendetyp = *Tändning av belysning*, Berörd enhet = the hero light, Motivering = *"Det är mörkt."* Submit.
3. **The receipt.** Kvittens: *"Din ansökan har mottagits. Diarienummer LV-2026-00xx. Du är nummer 4 i kön."*
4. **The decision.** Ärende page flips from *Under handläggning* to a formatted **beslut** — and **the light turns on, on camera.** (The payoff.)
5. **The lagom override.** New ansökan, full brightness → *delvis bifall*, capped, citing 3 §.
6. **The fika gag.** Show a Friday-14:30 submission → automatic *avslag*: *"Lampverket har fika."*
7. **The audit trail.** Mina ärenden / the diariet — every action stamped. Close on: *"…the same case-handling pattern real authorities use, in C#, pointed at my light switches."*

## Known issues — must fix before Block D

These are placeholder shortcuts that are acceptable during front-end TDD but will
break the real end-to-end flow the moment `Lampverket.Agent` is wired in.

| # | File | Issue | Required fix |
|---|------|-------|--------------|
| C3a | `src/Lampverket.Web/PlaceholderHandlaggareService.cs` | `RegisterAnsokanAsync` creates the `Arende` but never calls `IDiariet.AppendAsync` — `MinaArenden` is always empty | Real `HandlaggareService.RegisterAnsokanAsync` must call `IDiariet.AppendAsync(arende)` after assigning the diarienummer |
| C3b | `src/Lampverket.Web/PlaceholderHandlaggareService.cs` | `HamtaArendeAsync` always returns `null` — `ArendeDetalj` shows "Laddar…" forever after every submission | Real service must look up by diarienummer; also add a "not found" branch in `ArendeDetalj.razor` for unknown diarienummer |
| C4 | `src/Lampverket.Web/InMemoryDiariet.cs` | `List<Arende>` mutated from multiple concurrent Blazor Server circuits with no synchronisation (registered as `Singleton`) — concurrent `AppendAsync` calls race; `HamtaAllaAsync` can throw `InvalidOperationException` during a concurrent add | Wrap mutations in `lock (_lock)`, or replace `List<T>` with `ImmutableList` + `Interlocked` swap, before wiring live traffic |

## Stretch ideas (post-weekend)

- **Verksamhetsberättelse** — a scheduled job emitting a deadpan daily/annual report: ärenden received, beslut issued, average *handläggningstid*, *medborgarnöjdhet*.
- **Remiss** — for "big" requests (whole-flat movie mode), the agent sends the matter out for consultation and pretends to wait before deciding.
- **Two-agent split** — a *registrator* (intake + diarienummer) handing off to a *handläggare* (decision).
- **Diariet as MCP** — expose the diariet as an MCP resource/tool, so other agents can query Lampverket's case history. Doubles down on the MCP angle.
- **SQLite diariet** — move the log to EF Core + SQLite for Mina ärenden filtering.
- **"Two stacks" comparison** — document the same agent on a second platform, as a cross-vendor architecture note.
- **Mascot** — lean into the hero light. The banana 🍌 as the agency's unofficial seal.
