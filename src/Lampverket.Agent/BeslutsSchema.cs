using System.Text.Json;
using Anthropic.Models.Beta.Messages;

namespace Lampverket.Agent;

internal static class BeslutsSchema
{
    internal static InputSchema Build() => new()
    {
        Properties = new Dictionary<string, JsonElement>
        {
            ["beslutstyp"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                @enum = new[] { "Bifall", "DelvisBifall", "Avslag", "Avvisning" },
                description = "Beslutstyp: Bifall (beviljas), DelvisBifall (delvis), Avslag (avslås), Avvisning (avvisas formellt)."
            }),
            ["beslutstext"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Kortfattad beslutsmening, t.ex. 'Lampverket beviljar ansökan.'"
            }),
            ["motivering"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "1-3 meningar. Citera lagrum och förklara varför."
            }),
            ["lagrum"] = JsonSerializer.SerializeToElement(new
            {
                type = "array",
                items = new { type = "string" },
                description = "Citerade lagrum, t.ex. ['7 § lagen (2026:1) om skälig hemtrevnad']."
            }),
            ["overklagandehanvisning"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Anvisning om överklagande."
            }),
            ["verkstallighet"] = JsonSerializer.SerializeToElement(new
            {
                type = "string",
                description = "Vad ska utföras, eller varför ärendet bordläggs."
            }),
        },
        Required = ["beslutstyp", "beslutstext", "motivering", "lagrum", "overklagandehanvisning", "verkstallighet"],
    };
}
