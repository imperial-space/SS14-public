namespace Content.Server.Imperial.Heretic.Paths.Cosmos;

[RegisterComponent]
public sealed partial class HereticCosmicFieldComponent : Component
{
    [ViewVariables]
    public int PassiveLevel = 0;
    [ViewVariables]
    public HashSet<EntityUid> BoostedMobs = new();

    [DataField]
    public TimeSpan PushImmuneDelay = TimeSpan.FromSeconds(0.5);

    [DataField]
    public float UpdateRange = 0.6f;
}
