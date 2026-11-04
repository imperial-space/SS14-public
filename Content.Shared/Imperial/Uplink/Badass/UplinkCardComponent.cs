using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Одиночная карта из колоды Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkCardComponent : Component
{
    [DataField]
    public string CardName = string.Empty;

    [DataField]
    public bool FaceUp;
}
