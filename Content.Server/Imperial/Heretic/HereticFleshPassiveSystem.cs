using Content.Shared.Body;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Nutrition;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticFleshPassiveSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> MeatTag = "Meat";

    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly TagSystem        _tag        = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticFleshPassiveComponent, IngestingEvent>(OnIngesting);
    }

    public void ApplyPassiveLevel2(EntityUid uid)
    {
        EnsureComp<HereticFleshPassiveComponent>(uid);
    }

    public void ApplyPassiveLevel3(EntityUid uid)
    {
        var buff = EnsureComp<DamageProtectionBuffComponent>(uid);
        buff.Modifiers["HereticFleshBrute"] = new ProtoId<DamageModifierSetPrototype>("HereticFleshBrute");

        var stamRes = EnsureComp<StaminaResistanceComponent>(uid);
        stamRes.DamageCoefficient = 0.75f;
    }

    private void OnIngesting(Entity<HereticFleshPassiveComponent> ent, ref IngestingEvent args)
    {
        var food = args.Food;
        if (!_tag.HasTag(food, MeatTag) && !HasComp<OrganComponent>(food))
            return;

        var heal = new DamageSpecifier();
        heal.DamageDict["Blunt"]    = FixedPoint2.New(-15);
        heal.DamageDict["Slash"]    = FixedPoint2.New(-10);
        heal.DamageDict["Piercing"] = FixedPoint2.New(-5);
        _damageable.TryChangeDamage(ent.Owner, heal, true);
    }
}
