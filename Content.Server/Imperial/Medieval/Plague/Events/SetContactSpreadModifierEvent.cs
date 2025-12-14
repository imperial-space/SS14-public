using Content.Shared.Imperial.Medieval.Plague;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class SetContactSpreadModifierEvent : EntityEventArgs
{
    [DataField(required: true)]
    public float Modifier = 1f;
}
