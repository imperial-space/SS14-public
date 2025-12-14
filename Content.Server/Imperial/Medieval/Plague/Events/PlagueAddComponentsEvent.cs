using Content.Shared.Imperial.Medieval.Plague;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class PlagueAddComponentsEvent : EntityEventArgs
{
    [DataField(required: true)]
    public ComponentRegistry Components;

    [DataField]
    public bool Incubation = false;
}
