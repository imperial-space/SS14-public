using Content.Server.Atmos.Components;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticCosmosCloakSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly DamageSpecifier CurseDamage;

    static HereticCosmosCloakSystem()
    {
        CurseDamage = new DamageSpecifier();
        CurseDamage.DamageDict["Blunt"] = FixedPoint2.New(40);
    }

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCosmosCloakComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HereticCosmosCloakComponent, HereticCosmosCloakToggleLevitationEvent>(OnToggleLevitation);
        SubscribeLocalEvent<HereticCosmosCloakComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticCosmosCloakComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnGetActions(Entity<HereticCosmosCloakComponent> ent, ref GetItemActionsEvent args)
    {
        if (_inventory.InSlotWithFlags(ent.Owner, SlotFlags.OUTERCLOTHING))
        {
            args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnEquipped(Entity<HereticCosmosCloakComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
        Dirty(ent.Owner, ent.Comp);

        EnsureComp<PressureImmunityComponent>(args.Wearer);

        if (!HasComp<HereticComponent>(args.Wearer))
        {
            _damageable.TryChangeDamage(args.Wearer, CurseDamage, ignoreResistances: true);
            _popup.PopupEntity(Loc.GetString("heretic-cosmos-cloak-curse"), args.Wearer, args.Wearer, PopupType.LargeCaution);
        }
    }

    private void OnUnequipped(Entity<HereticCosmosCloakComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        var wearer = args.Wearer;

        ent.Comp.Wearer = null;

        RemComp<PressureImmunityComponent>(wearer);

        if (ent.Comp.IsLevitating)
        {
            RemComp<MovementIgnoreGravityComponent>(wearer);
            RemComp<CanMoveInAirComponent>(wearer);
            ent.Comp.IsLevitating = false;
        }

        _actions.RemoveAction(ent.Comp.ToggleActionEntity);
        ent.Comp.ToggleActionEntity = null;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnToggleLevitation(Entity<HereticCosmosCloakComponent> ent, ref HereticCosmosCloakToggleLevitationEvent args)
    {
        if (ent.Comp.Wearer is not { } wearer)
            return;

        args.Handled = true;
        ent.Comp.IsLevitating = !ent.Comp.IsLevitating;

        if (ent.Comp.IsLevitating)
        {
            var gravity = EnsureComp<MovementIgnoreGravityComponent>(wearer);
            gravity.Weightless = true;
            EnsureComp<CanMoveInAirComponent>(wearer);
            _popup.PopupEntity(Loc.GetString("heretic-cosmos-cloak-levitation-on"), wearer, wearer);
        }
        else
        {
            RemComp<MovementIgnoreGravityComponent>(wearer);
            RemComp<CanMoveInAirComponent>(wearer);
            _popup.PopupEntity(Loc.GetString("heretic-cosmos-cloak-levitation-off"), wearer, wearer);
        }

        Dirty(ent.Owner, ent.Comp);
    }
}
