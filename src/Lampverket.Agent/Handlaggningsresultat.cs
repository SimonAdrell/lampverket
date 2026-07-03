using Lampverket.Core;

namespace Lampverket.Agent;

/// <summary>
/// Vad agenten kom fram till: beslutet (Claudes rättsakt) plus det observerade verkställighetsutfallet.
/// C# stämmer sedan av dessa mot varandra i <see cref="Lampverket.Core.Verkstallighetsregler"/>.
/// </summary>
public sealed record Handlaggningsresultat(Beslut Beslut, Verkstallighetsstatus? Verkstallighetsutfall);
