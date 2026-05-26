namespace Lampverket.Core;

public interface IHandlaggareService
{
    Task<Arende> RegisterAnsokanAsync(Ansokan ansokan);
    Task<Arende?> HamtaArendeAsync(string diarienummer);
}
