using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Imperial.Heretic.Core;

[RegisterComponent]
public sealed partial class HereticAuraVisualsComponent : Component
{
    [DataField]
    public EntityUid? AuraEntity;

    [DataField]
    public bool HasEarnedAura;

    [DataField]
    public bool IsWearingRobe;
}
