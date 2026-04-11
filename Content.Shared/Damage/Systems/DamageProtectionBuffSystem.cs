using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

public sealed class DamageProtectionBuffSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageProtectionBuffComponent, DamageModifyEvent>(OnDamageModify);
    }

    private void OnDamageModify(EntityUid uid, DamageProtectionBuffComponent component, DamageModifyEvent args)
    {
        foreach (var modifierId in component.Modifiers.Values)
        {
            if (_proto.TryIndex(modifierId, out var modifier))
                args.Damage = DamageSpecifier.ApplyModifierSet(args.Damage, modifier);
        }
    }
}
