namespace Content.Server.Imperial.Lavaland.GraveKatana;

[RegisterComponent]
public sealed partial class GraveSkeletonSpawnerComponent : Component
{
    [DataField]
    public string SkeletonPrototype = "MobSkeletonLavalandGrave";
}
