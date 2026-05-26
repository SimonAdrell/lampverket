using Lampverket.HomeAssistant;
using Lampverket.HomeAssistant.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

// ---------------------------------------------------------------------------
// Lampverket.HomeAssistant.TryIt — opt-in smoke test + addressing spike.
// Requires user-secrets (dev) or env vars (ci/prod):
//   dotnet user-secrets set "HomeAssistant:BaseUrl" "http://homeassistant.local:8123"
//   dotnet user-secrets set "HomeAssistant:Token"   "<long-lived-access-token>"
//
// Set at least one device in config for a meaningful state read:
//   HomeAssistant__Devices__0__Friendly = Banan
//   HomeAssistant__Devices__0__EntityId = light.bedroom_banan
//   HomeAssistant__Devices__0__Area     = Bedroom
// ---------------------------------------------------------------------------

var config = new ConfigurationBuilder()
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>()
    .Build();

var baseUrl = config["HomeAssistant:BaseUrl"];
var token = config["HomeAssistant:Token"];

if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("[TryIt] Skipped — HomeAssistant:BaseUrl and HomeAssistant:Token not set.");
    Console.WriteLine("  Set via: dotnet user-secrets set \"HomeAssistant:BaseUrl\" \"http://...:8123\"");
    Console.WriteLine("           dotnet user-secrets set \"HomeAssistant:Token\"   \"<token>\"");
    return;
}

// Build DI container
var services = new ServiceCollection();
services.AddLogging(b => b.AddConsole().SetMinimumLevel(LogLevel.Information));
services.Configure<HomeAssistantOptions>(config.GetSection("HomeAssistant"));
services.AddSingleton<IMcpGateway, McpGateway>();

using var sp = services.BuildServiceProvider();
var gateway = sp.GetRequiredService<IMcpGateway>();
var opts = sp.GetRequiredService<IOptions<HomeAssistantOptions>>().Value;

Console.WriteLine($"\n[TryIt] Connected to: {baseUrl}{opts.McpEndpointPath}");

// 1. List tools
Console.WriteLine("\n--- Available tools ---");
var tools = await gateway.ListToolsAsync();
foreach (var t in tools)
    Console.WriteLine($"  {t.Name}: {t.Description}");

// 2. Addressing spike — try both friendly name and entity_id for the first configured device
if (opts.Devices.Length > 0)
{
    var device = opts.Devices[0];
    Console.WriteLine($"\n--- Addressing spike for '{device.Friendly}' (entity: {device.EntityId}) ---");

    Console.WriteLine($"\n> HassTurnOn with name: \"{device.Friendly}\" (friendly name)");
    var r1 = await gateway.CallToolAsync("HassTurnOn",
        new Dictionary<string, object?> { ["name"] = device.Friendly });
    Console.WriteLine($"  IsError={r1.IsError}  Content={r1.Content}");

    await Task.Delay(2000); // let HA settle

    Console.WriteLine($"\n> HassTurnOff with name: \"{device.Friendly}\" (friendly name — turning off again)");
    var r2 = await gateway.CallToolAsync("HassTurnOff",
        new Dictionary<string, object?> { ["name"] = device.Friendly });

    Console.WriteLine($"  IsError={r2.IsError}  Content={r2.Content}");

    Console.WriteLine($"\n> HassTurnOn with name: \"{device.EntityId}\" (entity_id)");
    var r3 = await gateway.CallToolAsync("HassTurnOn",
        new Dictionary<string, object?> { ["name"] = device.EntityId });
    Console.WriteLine($"  IsError={r3.IsError}  Content={r3.Content}");

    await Task.Delay(2000);

    Console.WriteLine($"\n> HassTurnOff with name: \"{device.EntityId}\" (entity_id — turning off again)");
    var r4 = await gateway.CallToolAsync("HassTurnOff",
        new Dictionary<string, object?> { ["name"] = device.EntityId });
    Console.WriteLine($"  IsError={r4.IsError}  Content={r4.Content}");

    // 3. GetLiveContext — raw output
    Console.WriteLine($"\n--- Raw GetLiveContext for '{device.Friendly}' ---");
    var ctx = await gateway.CallToolAsync("GetLiveContext",
        new Dictionary<string, object?> { ["name"] = device.Friendly });
    Console.WriteLine($"  IsError={ctx.IsError}");
    Console.WriteLine($"  Content (raw):\n{ctx.Content}");
}
else
{
    Console.WriteLine("\n[TryIt] No devices configured — skipping spike and GetLiveContext.");
    Console.WriteLine("  Add devices via HomeAssistant:Devices or appsettings.Local.json.");

    // Still print raw GetLiveContext with no filter to see all entities
    Console.WriteLine("\n--- Raw GetLiveContext (all entities) ---");
    var ctx = await gateway.CallToolAsync("GetLiveContext",
        new Dictionary<string, object?>());
    Console.WriteLine($"  IsError={ctx.IsError}");
    Console.WriteLine($"  Content (raw):\n{ctx.Content}");
}

Console.WriteLine("\n[TryIt] Done.");
