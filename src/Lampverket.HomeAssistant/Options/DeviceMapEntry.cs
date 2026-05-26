namespace Lampverket.HomeAssistant.Options;

public class DeviceMapEntry
{
    public string Friendly { get; set; } = "";
    public string Area { get; set; } = "";
    public string EntityId { get; set; } = "";
    /// <summary>
    /// Allowed actions for this device (e.g. "on", "off", "brightness", "volume", "play").
    /// Reserved for future use as a guard against calling unsupported operations
    /// (e.g. brightness on a media player). Not currently enforced by <see cref="HomeAssistantClient"/>.
    /// </summary>
    public string[] Actions { get; set; } = [];
}
