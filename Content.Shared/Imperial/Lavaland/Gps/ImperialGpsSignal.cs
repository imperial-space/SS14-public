using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland.Gps;

/// <summary>Сигнал GPS в списке (entrytag, coords, dist, degrees).</summary>
[Serializable, NetSerializable]
public sealed class ImperialGpsSignal
{
    public string Tag = string.Empty;
    public string Coords = string.Empty;
    public int? Distance;
    public int? Degrees;
}
