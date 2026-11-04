using Content.Shared.Damage;
using Content.Shared.Whitelist;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.NullRod.Components;

[DataDefinition]
public sealed partial class NullRodBane
{
    [DataField(required: true)]
    public EntityWhitelist Whitelist = new();

    [DataField(required: true)]
    public DamageSpecifier Damage = new();
}
