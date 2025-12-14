using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Inventory;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class SetPlagueCureEvent : EntityEventArgs
{
    [DataField(required: true)]
    public string Reagent = "";
}
