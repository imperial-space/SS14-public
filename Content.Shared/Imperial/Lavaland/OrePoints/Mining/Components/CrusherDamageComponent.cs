using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// status_effect/crusher_damage: сколько урона нанёс дробитель.
/// </summary>
[RegisterComponent]
public sealed partial class CrusherDamageComponent : Component
{
    [ViewVariables]
    public float TotalDamage;
}
