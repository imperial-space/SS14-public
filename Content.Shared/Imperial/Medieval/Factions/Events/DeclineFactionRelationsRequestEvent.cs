using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed partial class SetFactionRelationsByRequestEvent : EntityEventArgs
{
    public NetEntity Target;
    public bool Decline;

    public SetFactionRelationsByRequestEvent(NetEntity target, bool decline)
    {
        Target = target;
        Decline = decline;
    }
}
