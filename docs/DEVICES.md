# Devices — Home Assistant integration

Lampverket governs a small, explicit allow-list of Home Assistant devices. This document describes the **device model** and how the agent maps a citizen's request to a Home Assistant action.

> The devices below are **illustrative examples**. Replace them with your own entities in local config (see [Configuration](#configuration)). Real entity IDs are kept out of git.

## Requirements

- A running Home Assistant instance.
- Home Assistant's built-in **MCP Server** integration enabled and reachable from `Lampverket.HomeAssistant` (the C# MCP SDK connects to it to read state + call Assist tools).
- A long-lived access token (used by the MCP client to authenticate to Home Assistant), stored in **.NET user-secrets** (dev) or an environment variable (never committed).

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

Before deciding, `Lampverket.HomeAssistant` reads the current state of the affected device or area via the MCP live-context tool (`GetLiveContext`). This drives several personality rules:

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
