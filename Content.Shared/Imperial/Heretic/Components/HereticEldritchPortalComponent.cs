namespace Content.Shared.Imperial.Heretic.Components;

[RegisterComponent]
public sealed partial class HereticEldritchPortalComponent : Component
{
    public EntityUid? Caster;
    public EntityUid? LinkedPortal;
}
