namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticCosmicBeaconComponent : Component
{
    [DataField]
    public EntityUid? Caster;
}
