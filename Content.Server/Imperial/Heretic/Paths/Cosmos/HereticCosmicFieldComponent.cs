namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent]
public sealed partial class HereticCosmicFieldComponent : Component
{
    [ViewVariables]
    public int PassiveLevel = 0;
    [ViewVariables]
    public HashSet<EntityUid> BoostedMobs = new();
}
