namespace Lampverket.HomeAssistant.Options;

public class DeviceMapEntry
{
    public string Friendly { get; set; } = "";
    public string Area { get; set; } = "";
    public string EntityId { get; set; } = "";
    public string[] Actions { get; set; } = [];
}
