namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent]
public sealed partial class HereticEldritchPortalComponent : Component
{
    public EntityUid? Caster;
    public EntityUid? LinkedPortal;
}
