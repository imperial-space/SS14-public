using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>joint_torsion: заряжает батарею костюма при ходьбе.</summary>
[RegisterComponent]
public sealed partial class ImperialModJointTorsionComponent : Component
{
    /// <summary>power_per_step в долях DEFAULT_CHARGE_DRAIN за пройденный тайл.</summary>
    [DataField]
    public float PowerPerStep = 0.45f;

    [ViewVariables]
    public System.Numerics.Vector2? LastPosition;
}
