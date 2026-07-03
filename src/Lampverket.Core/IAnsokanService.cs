namespace Lampverket.Core;

public interface IAnsokanService
{
    Task<Arende> RegisterAnsokanAsync(Ansokan ansokan);
    Task<Arende?> HamtaArendeAsync(string diarienummer);
}
