using System.Linq;
using Content.Shared.FixedPoint;
using Content.Server.Popups;
using Content.Shared.Actions;
using Content.Shared.Clothing;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Imperial.ReactiveArmor;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Maps;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ReactiveArmor;

/// <summary>
/// Реактивная телепортационная броня 1 в 1 с tgstation/BandaStation (code/modules/clothing/suits/reactive_armour.dm).
/// </summary>
public sealed class ReactiveTeleportArmorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJointSystem _joints = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly TurfSystem _turf = default!;

    private static readonly SoundPathSpecifier BlinkSound = new("/Audio/Magic/blink.ogg");
    private static readonly SoundPathSpecifier BuzzSound = new("/Audio/Machines/buzz-sigh.ogg");

    /// <summary>Сколько раз пытаться найти клетку без стены, прежде чем сдаться.</summary>
    private const int TeleportAttempts = 20;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ReactiveTeleportArmorComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, GetItemActionsEvent>(OnGetActions);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, ToggleReactiveArmorEvent>(OnToggleAction);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, ClothingGotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, ClothingGotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnDamageModify);
        SubscribeLocalEvent<ReactiveTeleportArmorComponent, EmpPulseEvent>(OnEmp);

        SubscribeLocalEvent<ReactiveArmorShellComponent, InteractUsingEvent>(OnShellInteract);
    }

    private void OnMapInit(Entity<ReactiveTeleportArmorComponent> ent, ref MapInitEvent args)
    {
        _actions.AddAction(ent, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        UpdateVisuals(ent);
    }

    private void OnGetActions(Entity<ReactiveTeleportArmorComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
    }

    #region Переключение

    /// <summary>attack_self(): включить или выключить броню.</summary>
    private void OnUseInHand(Entity<ReactiveTeleportArmorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Toggle(ent, args.User);
    }

    private void OnToggleAction(Entity<ReactiveTeleportArmorComponent> ent, ref ToggleReactiveArmorEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        Toggle(ent, args.Performer);
    }

    private void Toggle(Entity<ReactiveTeleportArmorComponent> ent, EntityUid user)
    {
        ent.Comp.Active = !ent.Comp.Active;
        Dirty(ent);
        UpdateVisuals(ent);
        _actions.SetToggled(ent.Comp.ToggleActionEntity, ent.Comp.Active);

        _popup.PopupEntity(Loc.GetString(ent.Comp.Active ? "reactive-armor-active" : "reactive-armor-inactive", ("armor", ent.Owner)), user, user);
    }

    /// <summary>update_icon_state(): reactive / reactiveoff — и на руках, и на теле.</summary>
    private void UpdateVisuals(Entity<ReactiveTeleportArmorComponent> ent)
    {
        _appearance.SetData(ent, ReactiveArmorVisuals.Active, ent.Comp.Active);
        _clothing.SetEquippedPrefix(ent, ent.Comp.Active ? "on" : null);
    }

    private void OnEquipped(Entity<ReactiveTeleportArmorComponent> ent, ref ClothingGotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Wearer;
    }

    private void OnUnequipped(Entity<ReactiveTeleportArmorComponent> ent, ref ClothingGotUnequippedEvent args)
    {
        ent.Comp.Wearer = null;
    }

    #endregion

    #region Реакция на удар

    /// <summary>
    /// hit_reaction(): срабатывает на атаки — то есть на урон, у которого есть виновник.
    /// Пожар, разгерметизация и кровотечение броню не тревожат.
    /// </summary>
    private void OnDamageModify(Entity<ReactiveTeleportArmorComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!ent.Comp.Active || ent.Comp.Wearer is not { } wearer)
            return;

        var damage = args.Args;
        if (damage.Origin is not { } origin || origin == wearer || !damage.Damage.DamageDict.Values.Any(v => v > FixedPoint2.Zero))
            return;

        if (!_random.Prob(ent.Comp.ReactionChance))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextActivation)
        {
            // cooldown_activation()
            _popup.PopupEntity(Loc.GetString("reactive-teleport-armor-cooldown"), wearer, PopupType.MediumCaution);
            return;
        }

        ent.Comp.NextActivation = now + ent.Comp.Cooldown;

        if (now < ent.Comp.EmpEndsAt)
        {
            EmpActivation(ent, wearer, origin);
            return;
        }

        // reactive_activation(): владелец отброшен, атака не проходит.
        _popup.PopupEntity(Loc.GetString("reactive-teleport-armor-activate", ("wearer", wearer), ("attack", origin)), wearer, PopupType.MediumCaution);
        _audio.PlayPvs(BlinkSound, wearer);
        Teleport(wearer, ent.Comp.TeleportRange);

        var zeroed = new DamageSpecifier();
        foreach (var key in damage.Damage.DamageDict.Keys)
            zeroed.DamageDict[key] = FixedPoint2.Zero;
        damage.Damage = zeroed;
    }

    /// <summary>emp_activation(): броня спасается сама, бросая владельца под удар.</summary>
    private void EmpActivation(Entity<ReactiveTeleportArmorComponent> ent, EntityUid wearer, EntityUid origin)
    {
        _popup.PopupEntity(Loc.GetString("reactive-teleport-armor-emp-activate", ("attack", origin)), wearer, PopupType.LargeCaution);

        var coords = Transform(wearer).Coordinates;
        _audio.PlayPvs(BuzzSound, coords);
        _audio.PlayPvs(BlinkSound, coords);

        // dropItemToGround(src, TRUE, TRUE): атака при этом проходит.
        if (!_inventory.TryUnequip(wearer, "outerClothing", force: true, predicted: false))
            return;

        Teleport(ent, ent.Comp.TeleportRange);
    }

    private void OnEmp(Entity<ReactiveTeleportArmorComponent> ent, ref EmpPulseEvent args)
    {
        // emp_act(): уже поражённая или выключенная броня не реагирует.
        if (!ent.Comp.Active || _timing.CurTime < ent.Comp.EmpEndsAt)
            return;

        args.Affected = true;
        ent.Comp.EmpEndsAt = _timing.CurTime + ent.Comp.EmpDuration;

        var target = ent.Comp.Wearer ?? ent.Owner;
        _popup.PopupEntity(Loc.GetString("reactive-teleport-armor-emp"), target, PopupType.MediumCaution);
    }

    /// <summary>
    /// do_teleport(owner, get_turf(owner), tele_range): случайная клетка в квадрате ±tele_range,
    /// в безопасность или в ещё большую опасность — это никому не известно.
    /// </summary>
    private void Teleport(EntityUid uid, int range)
    {
        var xform = Transform(uid);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);

        for (var i = 0; i < TeleportAttempts; i++)
        {
            var tile = origin + new Vector2i(_random.Next(-range, range + 1), _random.Next(-range, range + 1));
            if (tile == origin)
                continue;

            // В стену телепорт не ставит, а вот в открытый космос — запросто.
            if (_map.TryGetTileRef(gridUid, grid, tile, out var tileRef)
                && !tileRef.Tile.IsEmpty
                && _turf.IsTileBlocked(tileRef, CollisionGroup.Impassable))
                continue;

            if (TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
                _pulling.TryStopPull(uid, pullable);

            _joints.RecursiveClearJoints(uid);
            _xform.AttachToGridOrMap(uid);
            _xform.SetCoordinates(uid, _map.GridTileToLocal(gridUid, grid, tile));
            return;
        }
    }

    #endregion

    #region Оболочка

    /// <summary>reactive_armor_shell/item_interaction(): ядро аномалии превращает оболочку в броню.</summary>
    private void OnShellInteract(Entity<ReactiveArmorShellComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        if (MetaData(args.Used).EntityPrototype?.ID is not { } coreId
            || !ent.Comp.Armors.TryGetValue(coreId, out var armorProto))
            return;

        args.Handled = true;

        var coords = Transform(ent).Coordinates;
        _popup.PopupEntity(Loc.GetString("reactive-armor-shell-insert", ("core", args.Used)), args.User, args.User);

        Spawn(armorProto, coords);
        QueueDel(args.Used);
        QueueDel(ent);
    }

    #endregion
}
