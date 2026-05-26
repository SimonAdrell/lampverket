using Lampverket.Agent;
using Lampverket.Core;
using Lampverket.HomeAssistant;
using Lampverket.Web;
using Lampverket.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddOutputCache();

builder.Services.AddScoped<IUserSession, UserSession>();
builder.Services.AddSingleton<IDiariet, InMemoryDiariet>();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.AddHomeAssistant();
builder.Services.AddAgent(builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseOutputCache();
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();

app.Run();
