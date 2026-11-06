using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/component/stove: греет стоящую на ней кастрюлю.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialStoveComponent : Component
{
    public const string PotSlot = "stove_pot";

    [DataField, AutoNetworkedField]
    public bool On;

    /// <summary>heat_coefficient.</summary>
    [DataField]
    public float HeatCoefficient = 0.033f;

    /// <summary>SOUP_BURN_TEMP + 80.</summary>
    [DataField]
    public float TargetTemperature = 620f;

    [DataField]
    public EntProtoId? StartingPot;
}
