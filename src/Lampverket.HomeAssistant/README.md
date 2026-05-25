# Lampverket.HomeAssistant

MCP client to Home Assistant's built-in MCP Server integration. Reads device state and executes actions on behalf of an already-issued *beslut*. No decision logic; no Anthropic/Claude dependency.

## Prerequisites

1. **Home Assistant** running and reachable.
2. **MCP Server integration** enabled in HA (Settings → Integrations → search "MCP Server").
3. A **long-lived access token** generated in your HA profile.

## Configuration

Secrets belong in **.NET user-secrets** (dev) or environment variables (prod) — never in committed config.

```bash
dotnet user-secrets set "HomeAssistant:BaseUrl" "http://homeassistant.local:8123"
dotnet user-secrets set "HomeAssistant:Token"   "<long-lived-access-token>"
```

Non-secret config goes in `appsettings.Local.json` (gitignored). See `appsettings.Example.json` for the shape.

### Device map

Map each Home Assistant entity you want to control:

```json
{
  "HomeAssistant": {
    "Devices": [
      {
        "Friendly": "Banan",
        "Area": "Bedroom",
        "EntityId": "light.banan",
        "Actions": ["on", "off", "brightness"]
      }
    ]
  }
}
```

`Friendly` is the name used in requests (e.g. the agent says "turn on Banan"). `EntityId` is what gets passed to the HA Assist tools.

## DI registration

```csharp
// Register and bind options
services.Configure<HomeAssistantOptions>(config.GetSection("HomeAssistant"));
services.AddHomeAssistant();

// Resolves IHomeAssistantClient → HomeAssistantClient (scoped)
//         IMcpGateway           → McpGateway (singleton, owns SSE connection)
```

## Public API

```csharp
IHomeAssistantClient client = ...;

DeviceState state  = await client.GetStateAsync("Banan");
HaResult turnOn    = await client.TurnOnAsync("Banan");
HaResult turnOff   = await client.TurnOffAsync("Banan");
HaResult setBright = await client.SetBrightnessAsync("Banan", 60);   // 0-100 %
HaResult setVol    = await client.SetVolumeAsync("Speaker", 40);     // 0-100 %
HaResult play      = await client.PlayMediaAsync("Speaker", "jazz");

IReadOnlyList<McpToolInfo> tools = await client.ListToolsAsync();
```

### Result types

`HaResult` is a discriminated union — no exceptions for expected outcomes:

| Variant | Meaning |
|---------|---------|
| `Ok()` | Action succeeded |
| `DeviceNotFound(Name)` | Friendly name not in device map |
| `DeviceUnavailable(Name)` | Device exists but HA reports it unavailable/unknown |
| `ToolError(Message)` | HA Assist tool returned `IsError: true` |

### `DeviceState` fields

| Field | Notes |
|-------|-------|
| `FriendlyName` | From device map |
| `EntityId` | From device map |
| `IsOn` | `state == 'on'` |
| `BrightnessPercent` | `null` when off; HA returns 0-255 on read — converted to 0-100 |
| `IsAvailable` | `false` when state is `unavailable` or `unknown` |

> **Brightness asymmetry** — HA Assist tools accept brightness as 0-100 (SET), but `GetLiveContext` returns raw 0-255 (READ). The client converts automatically in both directions.

## Running the TryIt smoke test

```bash
dotnet user-secrets --project src/Lampverket.HomeAssistant.TryIt set "HomeAssistant:BaseUrl" "http://..."
dotnet user-secrets --project src/Lampverket.HomeAssistant.TryIt set "HomeAssistant:Token"   "<token>"

# Add a device (optional — shows all devices via GetLiveContext if none configured)
# HomeAssistant__Devices__0__Friendly=Banan
# HomeAssistant__Devices__0__EntityId=light.banan
# HomeAssistant__Devices__0__Area=Bedroom

dotnet run --project src/Lampverket.HomeAssistant.TryIt
```

Exits cleanly with a skip message when secrets are absent (CI-safe).

The harness:
1. Lists all MCP tools advertised by HA.
2. **Addressing spike** — calls `HassTurnOn` with both the friendly name and the entity_id; prints results so you can confirm which resolves correctly.
3. Prints the raw `GetLiveContext` response for your hero device — use this to verify the parser against your instance.

## Test approach

Unit tests in `Lampverket.HomeAssistant.Tests` use a hand-rolled `FakeMcpGateway` that implements `IMcpGateway`. No live HA required. All test fixtures are captured from a real HA instance.

```bash
dotnet test src/Lampverket.HomeAssistant.Tests
```

## Verified HA facts (spike results, 2026-05-25)

| Question | Answer |
|----------|--------|
| HA MCP endpoint | `/mcp` |
| Tool names | `HassTurnOn`, `HassTurnOff`, `HassLightSet`, `HassSetVolume`, `HassMediaSearchAndPlay`, `GetLiveContext` |
| `name` arg — friendly or entity_id? | **Both work.** `"Banan"` and `"light.banan"` both resolve correctly. Client passes entity_id (unambiguous). |
| Hero light entity_id | `light.banan` (note: *not* `light.bedroom_banan`) |
| Brightness in `GetLiveContext` | Raw 0-255 (`'153'` for 60%) |
| Brightness in `HassLightSet` | 0-100 integer |
| `HassMediaSearchAndPlay` query param | `search_query` (not `query`) |

## What to verify against your own HA

- Run TryIt and confirm the addressing spike results match.
- Check `GetLiveContext` raw output matches the parser (especially for your light entities).
- Override `HomeAssistant:McpEndpointPath` if your HA MCP Server uses a different path (default `/mcp`).
