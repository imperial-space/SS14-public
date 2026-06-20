namespace Content.Server.Imperial.Lavaland.GraveKatana;

[RegisterComponent]
public sealed partial class GraveKatanaComponent : Component
{
    [DataField]
    public string KatanaPrototype = "WeaponKatanaLavaland";

    [DataField]
    public string EmptyGravePrototype = "GraveKatanaTombEmpty";

    [DataField]
    public float SkeletonSpawnRadius = 5f;
}
