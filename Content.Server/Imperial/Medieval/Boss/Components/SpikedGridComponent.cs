using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class SpikedGridComponent : Component
{
    [DataField]
    public float SpawnInterval = 1f;

    [DataField(required: true)]
    public EntProtoId SpikeProto;

    [DataField]
    public SpikedGridDirection Direction = SpikedGridDirection.Right;

    [DataField]
    public bool RandomDirection = true;

    [DataField]
    public int TargetIndex = 10;

    [DataField]
    public List<int> TargetIndexesPossible = new() { 24, 17 };

    public int NextIndex = 0;

    public TimeSpan NextSpawn = TimeSpan.Zero;
}

public enum SpikedGridDirection
{
    Right,
    Left,
    Up,
    Down
}
