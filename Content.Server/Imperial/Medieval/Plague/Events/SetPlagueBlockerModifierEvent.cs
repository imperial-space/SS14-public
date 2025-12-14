using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Inventory;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class SetPlagueBlockerModifierEvent : EntityEventArgs
{
    [DataField(required: true)]
    public float Modifier = 1f;
}
