using System.Linq;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Popups;
using Content.Shared.Stealth;
using Content.Shared.Stealth.Components;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticVoidWeaveSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStealthSystem _stealth = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private static readonly DamageSpecifier CurseDamage;

    static HereticVoidWeaveSystem()
    {
        CurseDamage = new DamageSpecifier();
        CurseDamage.DamageDict["Cold"] = FixedPoint2.New(60);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticVoidWeaveComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticVoidWeaveComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<HereticVoidWeaveComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticVoidWeaveComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.IsStealthed || comp.Wearer is not { } wearer)
                continue;

            if (now < comp.StealthEndTime)
                continue;

            RemComp<StealthComponent>(wearer);
            comp.IsStealthed = false;
            Dirty(uid, comp);
        }
    }

    private void OnEquipped(Entity<HereticVoidWeaveComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent.Owner, ent.Comp);

        if (!HasComp<HereticComponent>(args.Wearer))
        {
            _damageable.TryChangeDamage(args.Wearer, CurseDamage, ignoreResistances: true);
            _popup.PopupEntity(Loc.GetString("heretic-void-weave-curse"), args.Wearer, args.Wearer, PopupType.LargeCaution);
        }
    }

    private void OnUnequipped(Entity<HereticVoidWeaveComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var wearer = args.Wearer;

        if (ent.Comp.IsStealthed)
        {
            RemComp<StealthComponent>(wearer);
            ent.Comp.IsStealthed = false;
        }

        ent.Comp.Wearer = null;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnDamageModify(Entity<HereticVoidWeaveComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        if (!args.Args.Damage.DamageDict.Values.Any(v => v > FixedPoint2.Zero))
            return;

        if (!_random.Prob(ent.Comp.BlockChance))
            return;

        // Nullify the incoming attack completely
        var zeroed = new DamageSpecifier();
        foreach (var key in args.Args.Damage.DamageDict.Keys.ToList())
            zeroed.DamageDict[key] = FixedPoint2.Zero;
        args.Args.Damage = zeroed;

        _popup.PopupEntity(Loc.GetString("heretic-void-weave-block"), wearer, wearer, PopupType.Small);

        // Apply or extend brief stealth
        var stealthComp = EnsureComp<StealthComponent>(wearer);
        _stealth.SetVisibility(wearer, -1f, stealthComp);
        ent.Comp.IsStealthed = true;
        ent.Comp.StealthEndTime = _timing.CurTime + ent.Comp.StealthDuration;
        Dirty(ent.Owner, ent.Comp);
    }
}
