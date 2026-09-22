using Content.Server.Atmos.EntitySystems;
using Content.Shared.Actions;
using Robust.Shared.Timing;
using Content.Shared.Atmos.Components;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticScorchedMantleSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private static readonly DamageSpecifier BurnDamage;

    static HereticScorchedMantleSystem()
    {
        BurnDamage = new DamageSpecifier();
        BurnDamage.DamageDict["Heat"] = FixedPoint2.New(60);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticScorchedMantleComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HereticScorchedMantleComponent, HereticScorchedMantleToggleFlamesEvent>(OnToggleFlames);
        SubscribeLocalEvent<HereticScorchedMantleComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticScorchedMantleComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticScorchedMantleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.FlamesActive || comp.Wearer is not { } wearer)
                continue;

            if (now < comp.NextFlameTime)
                continue;

            comp.NextFlameTime = now + comp.FlameInterval;

            if (!TryComp<FlammableComponent>(wearer, out var flammable))
                continue;

            _flammable.AdjustFireStacks(wearer, 1f, flammable, ignite: true);
        }
    }

    private void OnGetActions(Entity<HereticScorchedMantleComponent> ent, ref GetItemActionsEvent args)
    {
        if (_inventory.InSlotWithFlags(ent.Owner, SlotFlags.OUTERCLOTHING))
        {
            args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnEquipped(Entity<HereticScorchedMantleComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent.Owner, ent.Comp);

        if (!HasComp<HereticComponent>(args.Wearer))
        {
            _damageable.TryChangeDamage(args.Wearer, BurnDamage, ignoreResistances: true);
            _popup.PopupEntity(Loc.GetString("heretic-scorched-mantle-burn"), args.Wearer, args.Wearer, PopupType.LargeCaution);
        }
    }

    private void OnUnequipped(Entity<HereticScorchedMantleComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var wearer = args.Wearer;

        ent.Comp.Wearer = null;
        ent.Comp.FlamesActive = false;

        if (TryComp<FlammableComponent>(wearer, out var flammable))
            _flammable.Extinguish(wearer, flammable);

        _actions.RemoveAction(ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnToggleFlames(Entity<HereticScorchedMantleComponent> ent, ref HereticScorchedMantleToggleFlamesEvent args)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        args.Handled = true;
        ent.Comp.FlamesActive = !ent.Comp.FlamesActive;

        if (ent.Comp.FlamesActive)
        {
            ent.Comp.NextFlameTime = _timing.CurTime + ent.Comp.FlameInterval;
            _popup.PopupEntity(Loc.GetString("heretic-scorched-mantle-flames-on"), wearer, wearer);
        }
        else
        {
            if (TryComp<FlammableComponent>(wearer, out var flammable))
                _flammable.Extinguish(wearer, flammable);
            _popup.PopupEntity(Loc.GetString("heretic-scorched-mantle-flames-off"), wearer, wearer);
        }

        Dirty(ent.Owner, ent.Comp);
    }

}
