using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRustArmorSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem  _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup      = default!;
    [Dependency] private readonly HereticSystem     _heretic    = default!;
    [Dependency] private readonly IGameTiming       _timing     = default!;

    private static readonly DamageSpecifier CurseDamage;

    static HereticRustArmorSystem()
    {
        CurseDamage = new DamageSpecifier();
        CurseDamage.DamageDict["Blunt"] = FixedPoint2.New(60);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRustArmorComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticRustArmorComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticRustArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticRustArmorComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Wearer is not { } wearer)
                continue;

            var coords = Transform(wearer).Coordinates;
            var onRust = _heretic.IsTileRusted(coords);

            if (onRust)
            {
                comp.GraceEndTime = now + comp.GraceDuration;
                if (!comp.IsOnRust)
                {
                    comp.IsOnRust = true;
                    Dirty(uid, comp);
                }
            }
            else if (comp.IsOnRust && now > comp.GraceEndTime)
            {
                comp.IsOnRust = false;
                Dirty(uid, comp);
            }
        }
    }

    private void OnEquipped(Entity<HereticRustArmorComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent.Owner, ent.Comp);

        if (!HasComp<HereticComponent>(args.Wearer))
        {
            _damageable.TryChangeDamage(args.Wearer, CurseDamage, ignoreResistances: true);
            _popup.PopupEntity(Loc.GetString("heretic-rust-armor-curse"), args.Wearer, args.Wearer, PopupType.LargeCaution);
        }
    }

    private void OnUnequipped(Entity<HereticRustArmorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
        ent.Comp.IsOnRust = false;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnDamageModify(Entity<HereticRustArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!ent.Comp.IsOnRust)
            return;

        var boosted = new DamageSpecifier();
        foreach (var (type, amount) in args.Args.Damage.DamageDict)
            boosted.DamageDict[type] = amount * 0.57f;
        args.Args.Damage = boosted;
    }
}
