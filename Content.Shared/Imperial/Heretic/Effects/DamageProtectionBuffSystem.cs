using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Heretic.Effects;

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
