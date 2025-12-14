namespace Content.Server.Imperial.Medieval.SkeletonInvasion;

public sealed partial class SkullBossStandCompletedEvent : EntityEventArgs
{
    public EntityUid Stand;
    public int Parts;
    public int PurifiedParts;

    public SkullBossStandCompletedEvent(EntityUid stand, int parts, int purifiedParts)
    {
        Stand = stand;
        Parts = parts;
        PurifiedParts = purifiedParts;
    }
}
