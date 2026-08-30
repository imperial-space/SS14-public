using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Aquila.Jump;

[Serializable, NetSerializable]
public sealed class PlayJumpAnimationEvent(NetEntity entity) : EntityEventArgs
{
    public readonly NetEntity Entity = entity;
}
