using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

/// <summary>
/// element/crusher_loot: трофей выпадает, если большую часть урона нанёс дробитель.
/// </summary>
[RegisterComponent]
public sealed partial class CrusherLootComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Trophies = new();

    /// <summary>drop_mod, проценты.</summary>
    [DataField]
    public float DropChance = 25f;

    /// <summary>guaranteed_drop: доля урона дробителем, при которой трофей выпадает всегда (мегафауна — 0.6).</summary>
    [DataField]
    public float? GuaranteedDrop;
}
