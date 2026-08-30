using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Aquila.Backflip;

[Serializable, NetSerializable]
public sealed class PlayBackflipAnimationEvent(NetEntity entity) : EntityEventArgs
{
    public readonly NetEntity Entity = entity;
}
