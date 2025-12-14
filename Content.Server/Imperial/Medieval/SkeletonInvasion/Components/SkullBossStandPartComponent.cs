namespace Content.Server.Imperial.Medieval.SkeletonInvasion;

[RegisterComponent]
public sealed partial class SkullBossStandPartComponent : Component
{
    [DataField]
    public int Idx = 0;

    [DataField]
    public bool Purified = false;
}
