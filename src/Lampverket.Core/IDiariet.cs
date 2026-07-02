namespace Lampverket.Core;

public interface IDiariet
{
    Task AppendAsync(Arende arende);
    Task<IReadOnlyList<Arende>> HamtaAllaAsync();
    Task<Arende?> HamtaAsync(string diarienummer);
    Task<string> AllokeraDiarienummerAsync(int year);
}
