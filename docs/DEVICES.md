# Devices — Home Assistant integration

Lampverket governs a small, explicit allow-list of Home Assistant devices. This document describes the **device model** and how the agent maps a citizen's request to a Home Assistant action.

> The devices below are **illustrative examples**. Replace them with your own entities in local config (see [Configuration](#configuration)). Real entity IDs are kept out of git.

## Requirements

- A running Home Assistant instance.
- Home Assistant's built-in **MCP Server** integration enabled and reachable from `Lampverket.Agent` (the C# MCP SDK + `Anthropic.Mcp` helper connect to it to list tools and forward Claude's tool calls).
- A long-lived access token (used by the MCP client to authenticate to Home Assistant), stored in **.NET user-secrets** (dev) or an environment variable (never committed).

## MCP integration notes

Operational details that aren't obvious from reading the SDK and that bit us once already — keep these documented so future-you doesn't rediscover them with `tcpdump`.

### MCP endpoint path

The MCP Server is exposed at different paths depending on how you reach the HA instance:

| Deployment | `HomeAssistant:McpEndpointPath` |
| --- | --- |
| Local HA (direct LAN, `http://homeassistant.local:8123`) | `/mcp` |
| Nabu Casa cloud (`https://*.ui.nabu.casa`) | `/api/mcp` |

Configured per environment in `appsettings.Local.json` / user-secrets. The default in code is `/mcp`.

### `GetLiveContext` response shape

`GetLiveContext` returns YAML, not JSON. Real fixture captured from a live HA instance:

```yaml
- names: Banan
  domain: light
  state: 'on'
  areas: Bedroom
  attributes:
    brightness: '153'
```

Notes:

- The `state` value is quoted (`'on'`, `'off'`).
- When the light is **off**, the `brightness:` line is still present but empty.
- States other than `on`/`off` (`unavailable`, `unknown`, missing entirely) reach Claude as the raw `GetLiveContext` payload; the system prompt instructs Claude to issue a *bordläggning*-style beslut when the entity is not on/off.

### Brightness scaling — read vs. write asymmetry

| Direction | Range | Notes |
| --- | --- | --- |
| **Read** (`GetLiveContext` → `attributes.brightness`) | 0–255 | Raw HA value. Divide by 255 then multiply by 100 to get percent. |
| **Write** (`HassLightSet` → `brightness` arg) | 0–100 | HA's MCP Assist tool accepts percent directly. |

In the current design Claude sees the raw `GetLiveContext` YAML (0–255 brightness) and writes back percent via `HassLightSet` (0–100). The system prompt documents the asymmetry; there is no C# wrapper translating either direction.

## Example device model

A representative home, grouped by area. Each entry lists the actions the device supports and the Home Assistant tool used.

| Friendly name (example) | Area | Domain | Actions | Tool(s) |
| --- | --- | --- | --- | --- |
| `Banan` (the hero light) | Bedroom | light | on/off, brightness | `HassTurnOn`, `HassTurnOff`, `HassLightSet` |
| `Bedroom ceiling` | Bedroom | light | on/off | `HassTurnOn`, `HassTurnOff` |
| `Living room ceiling` | Living Room | light | on/off, brightness | `HassTurnOn`, `HassTurnOff`, `HassLightSet` |
| `Couch lamp` | Living Room | light | on/off, brightness | `HassLightSet` |
| `Kitchen light` | Kitchen | light | on/off | `HassTurnOn`, `HassTurnOff` |
| `Living room speaker` | Living Room | media_player | volume, play | `HassSetVolume`, `HassMediaSearchAndPlay` |
| `Kitchen speaker` | Kitchen | media_player | volume, play | `HassSetVolume`, `HassMediaSearchAndPlay` |

A whimsically named device (e.g. a bedroom light called "Banan") is encouraged — it makes the demo land. Pick names that read well inside a formal beslut.

## Reading state

Before deciding, Claude calls `GetLiveContext` for the affected entity (instructed by the system prompt protocol). This is a prompt-level convention, not a C# invariant: `GetLiveContext` is exempt from the beslut guard, so nothing forces the read — the C# guard only enforces beslut-*before-action*, which is a separate rule. This drives several personality rules:

- **Already on?** → *avslag* (obehövligt ärende).
- **Unavailable?** → *bordläggning* in character.
- **Current brightness/volume** → informs lagom adjustments.

## Intent-to-tool mapping

| Citizen intent | Decision considerations | Home Assistant action |
| --- | --- | --- |
| "Turn on the {room} light" | already-on? evening (mysbelysning)? | `HassTurnOn` (or `HassLightSet` with a lagom brightness) |
| "Turn off the {room} light" | already off? | `HassTurnOff` |
| "Make it brighter / max" | 3 § lagom cap | `HassLightSet` with capped brightness → *delvis bifall* |
| "Dim the lights" | — | `HassLightSet` lower brightness |
| "Play {music} in {room}" | after 22:00 → grannhänsyn cap | `HassMediaSearchAndPlay` then `HassSetVolume` |
| "Turn it up" | after 22:00 → cap; grannelagsbestämmelser | `HassSetVolume` (capped) |

## Configuration

**Secrets** (Anthropic key, HA token) go in **.NET user-secrets** in development and environment variables in production — never in committed `appsettings.json`:

```bash
dotnet user-secrets set "Anthropic:ApiKey"    "sk-..."
dotnet user-secrets set "HomeAssistant:BaseUrl" "http://homeassistant.local:8123"
dotnet user-secrets set "HomeAssistant:Token"  "..."
```

The **device map** (friendly names → real entity IDs) lives in configuration. Keep your real one out of git as `appsettings.Local.json` (gitignored) and commit an `appsettings.Example.json` with placeholders so others can run the project.

`appsettings.Local.json` (gitignored — example shape):

```json
{
  "Authority": { "Name": "Lampverket", "Handlaggare": "Bo Sken" },
  "Devices": [
    { "Friendly": "Banan",               "Area": "Sovrum",     "EntityId": "light.bedroom_banan",      "Actions": ["on", "off", "brightness"] },
    { "Friendly": "Living room ceiling",  "Area": "Vardagsrum", "EntityId": "light.livingroom_ceiling", "Actions": ["on", "off", "brightness"] },
    { "Friendly": "Living room speaker",  "Area": "Vardagsrum", "EntityId": "media_player.livingroom",  "Actions": ["volume", "play"] }
  ]
}
```

These devices are illustrative — replace `EntityId`s with your own. The dropdown of "Berörd enhet" in the e-tjänst (see [`WEBAPP.md`](WEBAPP.md)) is populated from this map.

## Safety scope

Only lights and speakers (and similar non-critical entities) are in scope. Do **not** wire Lampverket to locks, alarms, heating that could be left off in winter, or anything where a deadpan *avslag* could cause real harm. The joke depends on the stakes being low.
