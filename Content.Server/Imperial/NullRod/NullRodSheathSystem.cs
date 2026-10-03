using Content.Shared.ActionBlocker;
using Content.Shared.Actions;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.NullRod.Components;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Mind;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.NullRod;

/// <summary>
/// Ножны катаны Ханзо и приём «Контратака» (blade_counter из SS13): святой выбирает цель, на мгновение
/// замирает, и если цель в ближайшие полторы секунды бьёт его, он выхватывает клинок и бьёт в ответ
/// втрое сильнее, отбивая удар.
/// </summary>
public sealed class NullRodSheathSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const string FullPrefix = "full";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<NullRodSheathComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<NullRodSheathComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<NullRodSheathComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<NullRodSheathComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<NullRodSheathComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<NullRodSheathComponent, NullRodCounterattackActionEvent>(OnCounterAction);
        SubscribeLocalEvent<NullRodCounterStanceComponent, AttackedEvent>(OnAttacked);
        SubscribeLocalEvent<NullRodCounterStanceComponent, DamageModifyEvent>(OnDamageModify);
        SubscribeLocalEvent<NullRodCounterStanceComponent, UpdateCanMoveEvent>(OnUpdateCanMove);
        SubscribeLocalEvent<NullRodCounterStanceComponent, ComponentShutdown>(OnStanceShutdown);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<NullRodCounterStanceComponent>();
        while (query.MoveNext(out var uid, out var stance))
        {
            if (stance.Immobile && now > stance.ImmobileUntil)
            {
                stance.Immobile = false;
                _actionBlocker.UpdateCanMove(uid);
            }

            if (now > stance.Expires)
                RemCompDeferred<NullRodCounterStanceComponent>(uid);
        }
    }

    private void OnUpdateCanMove(Entity<NullRodCounterStanceComponent> ent, ref UpdateCanMoveEvent args)
    {
        if (ent.Comp.Immobile)
            args.Cancel();
    }

    private void OnStanceShutdown(Entity<NullRodCounterStanceComponent> ent, ref ComponentShutdown args)
    {
        if (!ent.Comp.Immobile)
            return;

        ent.Comp.Immobile = false;
        _actionBlocker.UpdateCanMove(ent);
    }

    private void OnMapInit(Entity<NullRodSheathComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent, ref ent.Comp.ActionEntity, ent.Comp.Action);
        UpdateVisuals(ent);
    }

    /// <summary>Приём доступен, только когда ножны на поясе (action_slots в SS13).</summary>
    private void OnGetActions(Entity<NullRodSheathComponent> ent, ref GetItemActionsEvent args)
    {
        if (args.SlotFlags is { } flags && (flags & SlotFlags.BELT) != 0)
            args.AddAction(ent.Comp.ActionEntity);
    }

    private void OnInserted(Entity<NullRodSheathComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Slot)
            return;

        ent.Comp.LastResheath = _timing.CurTime;
        UpdateVisuals(ent);
    }

    private void OnRemoved(Entity<NullRodSheathComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ent.Comp.Slot)
            UpdateVisuals(ent);
    }

    private void OnExamined(Entity<NullRodSheathComponent> ent, ref ExaminedEvent args)
    {
        if (GetBlade(ent) != null)
            args.PushMarkup(Loc.GetString("null-rod-sheath-examine"));
    }

    private void UpdateVisuals(Entity<NullRodSheathComponent> ent)
    {
        var full = GetBlade(ent) != null;
        _appearance.SetData(ent, NullRodSheathVisuals.Full, full);
        _clothing.SetEquippedPrefix(ent, full ? FullPrefix : null);
        _item.SetHeldPrefix(ent, full ? FullPrefix : null);
    }

    private EntityUid? GetBlade(Entity<NullRodSheathComponent> ent)
    {
        return _itemSlots.TryGetSlot(ent, ent.Comp.Slot, out var slot) ? slot.Item : null;
    }

    private void OnCounterAction(Entity<NullRodSheathComponent> sheath, ref NullRodCounterattackActionEvent args)
    {
        if (args.Handled)
            return;

        var user = args.Performer;
        var target = args.Target;

        if (GetBlade(sheath) == null)
        {
            _popup.PopupEntity(Loc.GetString("null-rod-counter-empty"), user, user);
            return;
        }

        if (_timing.CurTime < sheath.Comp.LastResheath + sheath.Comp.ResheathCooldown)
        {
            _popup.PopupEntity(Loc.GetString("null-rod-counter-resheathed"), user, user);
            return;
        }

        if (target == user)
        {
            _popup.PopupEntity(Loc.GetString("null-rod-counter-self"), user, user);
            return;
        }

        if (!_mind.TryGetMind(target, out _, out _))
        {
            _popup.PopupEntity(Loc.GetString("null-rod-counter-unpredictable"), user, user);
            return;
        }

        args.Handled = true;

        var stance = EnsureComp<NullRodCounterStanceComponent>(user);
        stance.Target = target;
        stance.Sheath = sheath;
        stance.Expires = _timing.CurTime + sheath.Comp.CounterWindow;
        stance.Countered = false;

        // Святой замирает, но руки свободны: оглушение не дало бы выхватить клинок.
        stance.ImmobileUntil = _timing.CurTime + sheath.Comp.ImmobilizeTime;
        stance.Immobile = true;
        _actionBlocker.UpdateCanMove(user);
        _popup.PopupEntity(Loc.GetString("null-rod-counter-ready", ("user", user), ("sheath", sheath.Owner)),
            user,
            PopupType.MediumCaution);
    }

    private void OnAttacked(Entity<NullRodCounterStanceComponent> ent, ref AttackedEvent args)
    {
        var stance = ent.Comp;
        if (stance.Countered || args.User != stance.Target || _timing.CurTime > stance.Expires)
            return;

        if (!TryComp<NullRodSheathComponent>(stance.Sheath, out var sheathComp)
            || !_inventory.TryGetContainingSlot(stance.Sheath, out _)
            || Transform(stance.Sheath).ParentUid != ent.Owner
            || !_itemSlots.TryGetSlot(stance.Sheath, sheathComp.Slot, out var slot)
            || slot.Item is null
            || !_itemSlots.TryEject(stance.Sheath, slot, null, out var ejected, excludeUserAudio: true)
            || ejected is not { } blade)
        {
            return;
        }

        _hands.PickupOrDrop(ent, blade);

        stance.Countered = true;
        var attacker = args.User;
        if (TryComp<MeleeWeaponComponent>(blade, out var melee))
            _damageable.TryChangeDamage(attacker, melee.Damage * sheathComp.DamageMultiplier, origin: ent);

        _audio.PlayPvs(sheathComp.CounterSound, ent);
        _popup.PopupEntity(Loc.GetString("null-rod-counter-strike", ("user", ent.Owner), ("blade", blade), ("target", attacker)),
            ent,
            PopupType.LargeCaution);

        // Удачная контратака сбрасывает откат (COOLDOWN_RESET в SS13).
        _actions.ClearCooldown(sheathComp.ActionEntity);
    }

    /// <summary>Удар, на который ответили контратакой, отбит.</summary>
    private void OnDamageModify(Entity<NullRodCounterStanceComponent> ent, ref DamageModifyEvent args)
    {
        if (!ent.Comp.Countered || args.Origin != ent.Comp.Target)
            return;

        args.Damage = new DamageSpecifier();
        RemCompDeferred<NullRodCounterStanceComponent>(ent);
    }
}
