using Content.Shared.Clothing.Components;
using Content.Shared.Foldable;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Verbs;
using Content.Server.Imperial.HamMaggotson.PullableHelmet.Components;
using Content.Shared.Popups;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Content.Shared.Imperial.HamMaggotson.PullableHelmet;
using Robust.Shared.Audio.Systems;
using Content.Shared.Actions;
namespace Content.Server.Imperial.HamMaggotson.PullableHelmet.Systems;

public sealed class PullableHelmetSystem : EntitySystem
{
    [Dependency] private readonly ClothingSystem _clothingSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedItemSystem _itemSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PullableHelmetComponent, GetVerbsEvent<AlternativeVerb>>(AddPullVerb);
        SubscribeLocalEvent<PullableHelmetComponent, PullHelmetDoAfterEvent>(TryToggleHelmet);
        SubscribeLocalEvent<PullableHelmetComponent, PullHelmetActionEvent>(TryStartDoAfter);
        SubscribeLocalEvent<PullableHelmetComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<PullableHelmetComponent, GetItemActionsEvent>(OnGetActions);
    }

    private void AddPullVerb(EntityUid uid, PullableHelmetComponent component, GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Hands == null)
            return;

        AlternativeVerb verb = new()
        {
            Act = () => TryStartDoAfter(args.User, (uid, component)),
            Text = component.Toggled ? Loc.GetString(component.PullUpText) : Loc.GetString(component.PullDownText),
            Priority = component.Toggled ? 0 : 2,
        };

        args.Verbs.Add(verb);
    }

    private void TryStartDoAfter(EntityUid user, Entity<PullableHelmetComponent> ent)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            user,
            ent.Comp.Delay,
            new PullHelmetDoAfterEvent(),
            ent,
            ent)
        {
            NeedHand = true,
        });
    }

    private void TryStartDoAfter(Entity<PullableHelmetComponent> ent, ref PullHelmetActionEvent args)
    {
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager,
            args.Performer,
            ent.Comp.Delay,
            new PullHelmetDoAfterEvent(),
            ent,
            ent)
        {
            NeedHand = true,
        });
    }

    public void TryToggleHelmet(EntityUid uid, PullableHelmetComponent component, ref PullHelmetDoAfterEvent args)
    {
        var user = args.User;
        if (!_inventory.InSlotWithFlags(uid, component.RequiredFlags))
            return;
        component.Toggled = !component.Toggled;
        var newPrototype = component.Toggled ? component.ToggledPrototype : component.UntoggledPrototype;
        var sound = component.Toggled ? component.PullDownSound : component.PullUpSound;
        var newEntity = Spawn(newPrototype, Transform(uid).Coordinates);
        if (!TryComp<PullableHelmetComponent>(newEntity, out var hlm))
        {
            QueueDel(newEntity);
            return;
        }
        hlm.Toggled = component.Toggled;
        if (_inventory.TryGetContainingSlot(uid, out var slot))
        {
            _inventory.TryUnequip(user, slot.Name);
            _inventory.TryEquip(user, newEntity, slot.Name);
            QueueDel(uid);
            _audio.PlayPvs(sound, user);
        }
    }
    private void OnGetActions(EntityUid uid, PullableHelmetComponent component, GetItemActionsEvent args)
    {
        if (component.Action != null
            && (args.SlotFlags & component.RequiredFlags) == component.RequiredFlags)
        {
            args.AddAction(component.Action.Value);
        }
    }

    private void OnMapInit(EntityUid uid, PullableHelmetComponent component, MapInitEvent args)
    {
        if (_actionContainer.EnsureAction(uid, ref component.Action, out var action, component.PullAction))
            _actionsSystem.SetEntityIcon((component.Action.Value, action), uid);
    }
}
