using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Крамольный баллончик (traitor_spraycan): рисует скользкое граффити Синдиката 3x3.</summary>
[RegisterComponent]
public sealed partial class UplinkSpraycanComponent : Component
{
    [DataField]
    public EntProtoId RuneProto = "ImperialTraitorRune";

    [DataField]
    public TimeSpan DrawTime = TimeSpan.FromSeconds(3);

    [ViewVariables]
    public bool Expended;

    [ViewVariables]
    public bool Drawing;

    /// <summary>Граффити, которое рисуется сейчас.</summary>
    [ViewVariables]
    public EntityUid? Rune;

    /// <summary>Место нового граффити до появления контура.</summary>
    [ViewVariables]
    public Robust.Shared.Map.EntityCoordinates? Spot;
}
