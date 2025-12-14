using Content.Shared.Imperial.Medieval.Plague;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class SetStrapHealResistanceEvent : EntityEventArgs
{
    [DataField(required: true)]
    public int StrapResistance;

    [DataField(required: true)]
    public float HealMod;
}
