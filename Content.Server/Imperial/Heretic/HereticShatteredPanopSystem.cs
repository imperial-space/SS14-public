using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticShatteredPanopSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly DamageSpecifier KnifeDamage;

    static HereticShatteredPanopSystem()
    {
        KnifeDamage = new DamageSpecifier();
        KnifeDamage.DamageDict["Slash"] = FixedPoint2.New(250);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticShatteredPanopComponent, ClothingGotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<HereticShatteredPanopComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (HasComp<HereticComponent>(args.Wearer))
            return;

        _damageable.TryChangeDamage(args.Wearer, KnifeDamage, ignoreResistances: true);
        _popup.PopupEntity(Loc.GetString("heretic-shattered-panoply-curse"), args.Wearer, args.Wearer, PopupType.LargeCaution);
    }
}
