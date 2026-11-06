using Content.Shared.Actions;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Explosives;

/// <summary>Пицца-бомба (pizzabox/bomb): при первом открытии ставится таймер, при следующем — обратный отсчёт.</summary>
[RegisterComponent]
public sealed partial class UplinkPizzaBombComponent : Component
{
    [DataField]
    public int Timer = 1;

    [ViewVariables]
    public bool Armed;

    [ViewVariables]
    public bool Open;

    [ViewVariables]
    public TimeSpan? DetonateAt;

    [DataField]
    public float UpdateTotalIntensity = 80f;

    [DataField]
    public float UpdateSlope = 4f;

    [DataField]
    public float UpdateMaxTileIntensity = 12f;
}
