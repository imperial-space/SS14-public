using Content.Shared.Imperial.Medieval.Plague;
using Content.Shared.Inventory;

namespace Content.Server.Imperial.Medieval.Plague;

[DataDefinition]
public sealed partial class SetPlagueMinSmellLevelEvent : EntityEventArgs
{
    [DataField(required: true)]
    public float Smell = 1f;
}
