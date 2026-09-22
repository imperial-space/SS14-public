using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Gravity;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Inventory;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticProphetGreavesSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticProphetGreavesComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<HereticProphetGreavesComponent, HereticGravityToggleActionEvent>(OnGravityToggle);
        SubscribeLocalEvent<HereticProphetGreavesComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<HereticProphetGreavesComponent, ClothingGotUnequippedEvent>(OnUnequipped);
    }

    private void OnGetActions(Entity<HereticProphetGreavesComponent> ent, ref GetItemActionsEvent args)
    {
        if (_inventory.InSlotWithFlags(ent.Owner, SlotFlags.FEET))
        {
            args.AddAction(ref ent.Comp.GravityToggleActionEntity, ent.Comp.GravityToggleAction);
            Dirty(ent.Owner, ent.Comp);
        }
    }

    private void OnEquipped(Entity<HereticProphetGreavesComponent> ent, ref ClothingGotEquippedEvent args)
    {
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnUnequipped(Entity<HereticProphetGreavesComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        if (ent.Comp.WeightlessEnabled)
        {
            if (TryComp<MovementIgnoreGravityComponent>(args.Wearer, out var gravity))
            {
                gravity.Weightless = false;
                Dirty(args.Wearer, gravity);
            }
            _gravity.RefreshWeightless(args.Wearer);
            ent.Comp.WeightlessEnabled = false;
        }
        _actions.RemoveAction(ent.Comp.GravityToggleActionEntity);
        ent.Comp.GravityToggleActionEntity = null;
        Dirty(ent.Owner, ent.Comp);
    }

    private void OnGravityToggle(Entity<HereticProphetGreavesComponent> ent, ref HereticGravityToggleActionEvent args)
    {
        args.Handled = true;
        var wearer = args.Performer;
        ent.Comp.WeightlessEnabled = !ent.Comp.WeightlessEnabled;
        var gravity = EnsureComp<MovementIgnoreGravityComponent>(wearer);
        gravity.Weightless = ent.Comp.WeightlessEnabled;
        Dirty(wearer, gravity);
        _gravity.RefreshWeightless(wearer);
        Dirty(ent.Owner, ent.Comp);
        var msgKey = ent.Comp.WeightlessEnabled
            ? "heretic-prophet-greaves-gravity-off"
            : "heretic-prophet-greaves-gravity-on";
        _popup.PopupEntity(Loc.GetString(msgKey), wearer, wearer);
    }
}
