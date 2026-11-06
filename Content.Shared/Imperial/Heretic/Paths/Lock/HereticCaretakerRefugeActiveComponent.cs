namespace Content.Shared.Imperial.Heretic.Paths.Lock;

[RegisterComponent]
public sealed partial class HereticCaretakerRefugeActiveComponent : Component
{
    [ViewVariables]
    public List<(string Id, bool Hard, int Layer, int Mask)> FixtureStates = new();
    [ViewVariables]
    public EntityUid? MainActionEntity;
    [ViewVariables]
    public EntityUid? ExitActionEntity;
    [ViewVariables]
    public bool AddedStealth;

    [DataField]
    public TimeSpan DeactivateRefugeDelay = TimeSpan.FromSeconds(60);
}
