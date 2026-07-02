# TODO — Code Clarity

## High Impact

- [x] **Extract guard branches in `GuardHaTool`** (`HaToolFactory.cs:27-83`) — three cases (query pass-through, action before beslut, action after non-approving beslut) are nested `if`s with no names. Extract into named private methods (`IsActionBlockedNoBeslut`, `IsActionBlockedByDecision`); lambda becomes a 4-line dispatcher.

- [x] **Rename `_queryTools` → `_beslutExemptTools`** (`HaToolFactory.cs:10`) — `{ "GetLiveContext" }` determines which tools skip the beslut guard, but "query" doesn't signal that. Name should express the exemption contract.

## Medium Impact

- [x] **Name the tool-setup pipeline in `HandlaggaAsync`** (`HandlaggareAgent.cs:49-60`) — filter → slot → build is spread over three lines with no named intent. Extract into `BuildGuardedToolSet(haTools, device, arende)` returning tools + slot; makes the guard-wrapping purpose explicit at the callsite.

- [ ] **Document write-once contract on `BeslutSlot`** (`BeslutSlot.cs`) — `Interlocked.CompareExchange` is the hint but buried. Add: `// write-once: first lamna_beslut call wins; subsequent calls rejected`.

- [ ] **Comment `TillaterVerkstallighet` usage in guard** (`HaToolFactory.cs:45`) — semantics (true only on bifall/delvis bifall) live in `Beslut`; readers shouldn't have to chase. Add a note at the usage site or inline the condition.

## Low Impact

- [x] **Signal the two-path design in `HandlaggareService`** (`HandlaggareService.cs`) — `RegisterAnsokanAsync` is fast-path (register + enqueue); `ProcessArendeAsync` is the agent path (called by background worker). The asymmetry is intentional but uncommented. Short structural comment at each method.
