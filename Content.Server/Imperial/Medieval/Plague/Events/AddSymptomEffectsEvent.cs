using Content.Shared.Imperial.Medieval.Plague;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class AddSymptomEffectsEvent : EntityEventArgs
{
    [DataField(required: true)]
    public string Id = default!;

    [DataField(required: true)]
    public BasePlagueEffect Effect = default!;

    [DataField]
    public bool Incubation = false;
}
