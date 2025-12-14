using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class SpikeAttackMarkerComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Proto;

    [DataField(required: true)]
    public Dictionary<int, List<Vector2i>> Positions;

    [DataField]
    public float SpawnInterval = 1f;

    [ViewVariables(VVAccess.ReadWrite)]
    public int Idx = 0;

    [ViewVariables(VVAccess.ReadWrite)]
    public int TargetIdx => Positions.Count - 1;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextSpawn = TimeSpan.Zero;
}
