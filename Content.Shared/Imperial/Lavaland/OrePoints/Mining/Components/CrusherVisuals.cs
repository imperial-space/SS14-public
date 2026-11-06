using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;

[Serializable, NetSerializable]
public enum CrusherVisuals : byte
{
    /// <summary>retool_kit/ashenskull: облик черепа.</summary>
    Skull,
}
