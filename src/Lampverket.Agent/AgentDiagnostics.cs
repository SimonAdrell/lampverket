using System.Diagnostics;

namespace Lampverket.Agent;

/// <summary>
/// OpenTelemetry-källa för handläggningen. Det omslutande spannet <c>handläggning</c> får separata
/// under-spann för context, decision och execution så Aspire visar fasernas tider och decision TTFT.
/// Källnamnet registreras i ServiceDefaults (AddSource).
/// </summary>
internal static class AgentDiagnostics
{
    public const string SourceName = "Lampverket.Agent";

    public static readonly ActivitySource Source = new(SourceName);
}
