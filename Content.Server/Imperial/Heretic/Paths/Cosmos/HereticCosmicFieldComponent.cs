namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent]
public sealed partial class HereticCosmicFieldComponent : Component
{
    public int PassiveLevel = 0;
    public HashSet<EntityUid> BoostedMobs = new();
}

[RegisterComponent]
public sealed partial class CosmicFieldBoostedComponent : Component
{
    public int FieldCount = 0;
}
