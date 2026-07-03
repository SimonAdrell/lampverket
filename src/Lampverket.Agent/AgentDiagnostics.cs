using System.Diagnostics;

namespace Lampverket.Agent;

/// <summary>
/// OpenTelemetry-källa för handläggningen. Spannet <c>handläggning</c> omsluter hela loopen så att
/// de annars föräldralösa Anthropic-anropen nästlas under det, och stegen (Granskar hemförhållanden,
/// Beslut fattat, Verkställer) läggs som tidsstämplade events — då syns beslutet ~8 s före loopens slut
/// direkt i Aspire-tracen. Källnamnet registreras i ServiceDefaults (AddSource).
/// </summary>
internal static class AgentDiagnostics
{
    public const string SourceName = "Lampverket.Agent";

    public static readonly ActivitySource Source = new(SourceName);
}
