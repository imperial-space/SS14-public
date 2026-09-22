using Robust.Shared.GameObjects;

namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticCaretakerRefugeActiveComponent : Component
{
    public List<(string Id, bool Hard, int Layer, int Mask)> FixtureStates = new();
    public EntityUid? MainActionEntity;
    public EntityUid? ExitActionEntity;
    public bool AddedStealth;
}
