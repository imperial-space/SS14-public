using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Boss;

[Serializable, NetSerializable]
public enum BossStageVisuals : byte
{
    Stage,
    Layer
}

[Serializable, NetSerializable]
public enum AdditionalBossVisuals : byte
{
    State,
    Layer
}
