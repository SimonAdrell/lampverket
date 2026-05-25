namespace Lampverket.HomeAssistant.Models;

public record DeviceState(
    string FriendlyName,
    string EntityId,
    bool IsOn,
    int? BrightnessPercent,
    bool IsAvailable);
