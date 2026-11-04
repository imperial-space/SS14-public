namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticEldritchPortalComponent : Component
{
    [ViewVariables]
    public EntityUid? Caster;
    [ViewVariables]
    public EntityUid? LinkedPortal;
}
