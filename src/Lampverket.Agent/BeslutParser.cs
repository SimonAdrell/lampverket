using System.Text.Json;
using System.Text.Json.Serialization;
using Lampverket.Core;
using Microsoft.Extensions.Logging;

namespace Lampverket.Agent;

internal static class BeslutParser
{
    public static Beslut? TryParse(
        IReadOnlyDictionary<string, JsonElement> input,
        DateTimeOffset datum,
        string diarienummer,
        ILogger logger)
    {
        try
        {
            var element = JsonSerializer.SerializeToElement(input);
            var dto = element.Deserialize<BeslutDto>();

            if (dto is null)
            {
                return null;
            }

            return dto.Beslutstyp?.ToLowerInvariant() switch
            {
                "bifall" => new Bifall(
                    dto.Beslutstext ?? "", dto.Motivering ?? "", dto.Lagrum ?? [],
                    dto.Overklagandehanvisning ?? "", dto.Verkstallighet ?? "", datum),
                "delvisbifall" => new DelvisBifall(
                    dto.Beslutstext ?? "", dto.Motivering ?? "", dto.Lagrum ?? [],
                    dto.Overklagandehanvisning ?? "", dto.Verkstallighet ?? "", datum),
                "avslag" => new Avslag(
                    dto.Beslutstext ?? "", dto.Motivering ?? "", dto.Lagrum ?? [],
                    dto.Overklagandehanvisning ?? "", datum),
                "avvisning" => new Avvisning(
                    dto.Beslutstext ?? "", dto.Motivering ?? "", dto.Lagrum ?? [],
                    dto.Overklagandehanvisning ?? "", datum),
                _ => null
            };
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to deserialize beslut payload for {Diarienummer}", diarienummer);
            return null;
        }
    }

    private sealed record BeslutDto(
        [property: JsonPropertyName("beslutstyp")] string? Beslutstyp,
        [property: JsonPropertyName("beslutstext")] string? Beslutstext,
        [property: JsonPropertyName("motivering")] string? Motivering,
        [property: JsonPropertyName("lagrum")] string[]? Lagrum,
        [property: JsonPropertyName("overklagandehanvisning")] string? Overklagandehanvisning,
        [property: JsonPropertyName("verkstallighet")] string? Verkstallighet
    );
}
