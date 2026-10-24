using System.Linq;
using System.Numerics;
using Content.Server.Chat.Systems;
using Content.Server.Ghost;
using Content.Shared.Access.Components;
using Content.Shared.Chat;
using Content.Shared.Access.Systems;
using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Content.Shared.DragDrop;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Fugitives;
using Content.Shared.Implants;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fugitives;

/// <summary>Действие маскировки выдано (implant/camouflage).</summary>
[RegisterComponent]
public sealed partial class ImperialCamouflageImplantComponent : Component
{
    [ViewVariables]
    public EntityUid? Action;
}

/// <summary>Стабилизированный Сатурн-X: невидимость до момента <see cref="Until"/>.</summary>
[RegisterComponent]
public sealed partial class ImperialInvisibilityComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>/obj/item/reagent_containers/hypospray/medipen/invisibility.</summary>
[RegisterComponent]
public sealed partial class ImperialInvisibilityInjectorComponent : Component
{
    /// <summary>volume 20: «примерно 10 минут».</summary>
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(10);
}

/// <summary>/obj/machinery/fugitive_locator.</summary>
[RegisterComponent]
public sealed partial class ImperialFugitiveLocatorComponent : Component
{
    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(40);

    [ViewVariables]
    public TimeSpan NextLocate;
}

/// <summary>Психическая проекция: цель в ужасе палит из оружия в руках.</summary>
[RegisterComponent]
public sealed partial class ImperialPsychicProjectedComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;

    [ViewVariables]
    public TimeSpan NextShot;
}

/// <summary>Психический ускоритель: двойная скорострельность на 10 секунд.</summary>
[RegisterComponent]
public sealed partial class ImperialPsychicBoostedComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>Снаряжение и машины беглецов и охотников TG (fugitive_equipment.dm, hunter_gear.dm, psyker.dm).</summary>
public sealed class ImperialFugitiveGearSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedMindSystem _mind = default!;
    [Dependency] private readonly GhostSystem _ghost = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly SharedSubdermalImplantSystem _implants = default!;
    [Dependency] private readonly SharedGunSystem _guns = default!;

    private const string PrisonContainer = "imperial_fugitive_prison";
    private static readonly EntProtoId KnockAction = "ActionKnock";
    private static readonly EntProtoId CamouflageAction = "ImperialActionCamouflage";
    private static readonly EntProtoId[] PsykerActions = { "ImperialActionPsychicProjection", "ImperialActionPsychicBooster", "ImperialActionPsychicWall" };
    private static readonly EntProtoId PsychicWall = "ImperialPsychicForcefieldTemporary";

    /// <summary>russian_hunter/pre_equip(): немного разнообразия в стиле.</summary>
    private static readonly EntProtoId[] RussianUniforms = { "ImperialClothingUniformSoviet", "ClothingUniformJumpsuitOperative", "ClothingUniformJumpsuitMinuteManCamoForest" };
    private static readonly EntProtoId[] RussianSuits = { "ClothingOuterArmorBasic", "ClothingOuterCoatGentle" };
    private static readonly EntProtoId[] RussianHelmets = { "ClothingHeadHatUshanka", "USSPSoldierUshanka", "ClothingHeadHelmetBasic" };

    /// <summary>mi13_hunter/pre_equip(): pick_weight по трём группам.</summary>
    private static readonly (EntProtoId Id, int Weight)[][] Mi13Loot =
    {
        new (EntProtoId, int)[] { ("MagazinePistol", 80), ("MagazinePistolPractice", 10), ("MagazinePistolIncendiary", 10) },
        new (EntProtoId, int)[] { ("EnergyDagger", 40), ("CombatKnife", 30), ("Flash", 30) },
        new (EntProtoId, int)[] { ("C4", 20), ("FreedomImplanter", 20), ("ClothingMaskGasChameleon", 20), ("ClothingEyesGlassesThermal", 20), ("ClothingEyesChameleon", 20) },
    };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialCamouflageImplantComponent, ImperialCamouflageActionEvent>(OnCamouflage);
        SubscribeLocalEvent<ImperialCamouflagedComponent, EmpPulseEvent>(OnCamouflageEmp);
        SubscribeLocalEvent<ImperialInvisibilityInjectorComponent, UseInHandEvent>(OnInjectorUse);

        SubscribeLocalEvent<ImperialFugitiveCaptureComponent, DragDropTargetEvent>(OnCaptureDrop);
        SubscribeLocalEvent<ImperialFugitiveCaptureComponent, ImperialFugitiveCaptureDoAfterEvent>(OnCaptureDoAfter);
        SubscribeLocalEvent<ImperialFugitiveLocatorComponent, ActivateInWorldEvent>(OnLocate);

        SubscribeLocalEvent<HumanoidProfileComponent, ImperialPsychicProjectionActionEvent>(OnProjection);
        SubscribeLocalEvent<HumanoidProfileComponent, ImperialPsychicBoosterActionEvent>(OnBooster);
        SubscribeLocalEvent<HumanoidProfileComponent, ImperialPsychicWallActionEvent>(OnPsychicWall);
        SubscribeLocalEvent<GunComponent, GunRefreshModifiersEvent>(OnGunRefresh);
    }

    #region Беглецы

    /// <summary>Вальдо: aoe/knock — ключ ко всем дверям.</summary>
    public void GrantKnock(EntityUid body)
    {
        _actions.AddAction(body, KnockAction);
    }

    /// <summary>invisible_man/post_equip(): экспериментальный маскировочный имплант.</summary>
    public void GrantCamouflage(EntityUid body)
    {
        var implant = EnsureComp<ImperialCamouflageImplantComponent>(body);
        _actions.AddAction(body, ref implant.Action, CamouflageAction);
    }

    private void OnCamouflage(Entity<ImperialCamouflageImplantComponent> ent, ref ImperialCamouflageActionEvent args)
    {
        args.Handled = true;
        if (HasComp<ImperialCamouflagedComponent>(ent) && !HasComp<ImperialInvisibilityComponent>(ent))
        {
            RemoveCamouflage(ent);
            return;
        }

        EnsureComp<ImperialCamouflagedComponent>(ent);
        _popup.PopupEntity(Loc.GetString("imperial-fugitive-camouflage-on"), ent, ent);
    }

    /// <summary>remove_cloaking(): искры и снова видим.</summary>
    private void RemoveCamouflage(EntityUid uid)
    {
        RemComp<ImperialCamouflagedComponent>(uid);
        Spawn("EffectSparks", Transform(uid).Coordinates);
        _popup.PopupEntity(Loc.GetString("imperial-fugitive-camouflage-off"), uid, uid);
    }

    /// <summary>implant/camouflage/emp_act(): 15% × сила — перегрузка.</summary>
    private void OnCamouflageEmp(Entity<ImperialCamouflagedComponent> ent, ref EmpPulseEvent args)
    {
        if (!HasComp<ImperialCamouflageImplantComponent>(ent) || HasComp<ImperialInvisibilityComponent>(ent) || !_random.Prob(0.3f))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-fugitive-camouflage-emp"), ent, Content.Shared.Popups.PopupType.MediumCaution);
        RemoveCamouflage(ent);
    }

    /// <summary>Автоинъектор невидимости: стабилизированный Сатурн-X почти на 10 минут.</summary>
    private void OnInjectorUse(Entity<ImperialInvisibilityInjectorComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var user = args.User;
        var invisible = EnsureComp<ImperialInvisibilityComponent>(user);
        invisible.Until = _timing.CurTime + ent.Comp.Duration;
        var camo = EnsureComp<ImperialCamouflagedComponent>(user);
        camo.Alpha = 0.03f;
        Dirty(user, camo);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/hypospray.ogg"), user);
        _popup.PopupEntity(Loc.GetString("imperial-fugitive-invisibility-on"), user, user);
        QueueDel(ent);
    }

    #endregion

    #region Машины охотников

    /// <summary>fugitive_capture/mouse_drop_receive().</summary>
    private void OnCaptureDrop(Entity<ImperialFugitiveCaptureComponent> ent, ref DragDropTargetEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (!IsWantedFugitive(args.Dragged))
        {
            _popup.PopupEntity(Loc.GetString("imperial-fugitive-capture-not-wanted"), ent, args.User);
            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, args.User, ent.Comp.CaptureTime, new ImperialFugitiveCaptureDoAfterEvent(), ent, args.Dragged, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnCaptureDoAfter(Entity<ImperialFugitiveCaptureComponent> ent, ref ImperialFugitiveCaptureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target is not { } target || !IsWantedFugitive(target))
            return;

        args.Handled = true;
        AddPrisoner(ent, target);
    }

    private bool IsWantedFugitive(EntityUid body)
    {
        return _mind.TryGetMind(body, out var mindId, out _)
               && TryComp<ImperialFugitiveMindComponent>(mindId, out var fugitive) && !fugitive.Captured;
    }

    /// <summary>add_prisoner(): беглец исчезает в блюспейсе навсегда.</summary>
    private void AddPrisoner(EntityUid machine, EntityUid fugitive)
    {
        if (!_mind.TryGetMind(fugitive, out var mindId, out var mind) || !TryComp<ImperialFugitiveMindComponent>(mindId, out var data))
            return;

        var prison = _containers.EnsureContainer<Container>(machine, PrisonContainer);
        _containers.Insert(fugitive, prison, force: true);
        data.Captured = true;
        _popup.PopupEntity(Loc.GetString("imperial-fugitive-captured"), fugitive, fugitive, Content.Shared.Popups.PopupType.LargeCaution);
        _ghost.OnGhostAttempt(mindId, false, forced: true, mind: mind);
    }

    /// <summary>fugitive_locator/interact(): имя и зона случайного живого беглеца раз в 40 секунд.</summary>
    private void OnLocate(Entity<ImperialFugitiveLocatorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        if (_timing.CurTime < ent.Comp.NextLocate)
        {
            _popup.PopupEntity(Loc.GetString("imperial-fugitive-locator-recharging"), ent, args.User);
            return;
        }

        ent.Comp.NextLocate = _timing.CurTime + ent.Comp.Cooldown;
        var bounty = LocateFugitive();
        var message = bounty is { } found
            ? Loc.GetString("imperial-fugitive-locator-found", ("name", Name(found)), ("area", AreaName(found)))
            : Loc.GetString("imperial-fugitive-locator-none");
        _chat.TrySendInGameICMessage(ent, message, InGameICChatType.Speak, false, ignoreActionBlocker: true);
    }


    /// <summary>get_area_name(): название ближайшего навигационного маяка.</summary>
    public string AreaName(EntityUid uid)
    {
        var pos = _transform.GetMapCoordinates(uid);
        var best = Loc.GetString("imperial-fugitive-unknown-area");
        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<Content.Shared.Pinpointer.NavMapBeaconComponent, TransformComponent>();
        while (query.MoveNext(out var beaconUid, out var beacon, out var xform))
        {
            if (!beacon.Enabled || xform.MapID != pos.MapId)
                continue;

            var name = beacon.Text ?? (beacon.DefaultText is { } loc ? Loc.GetString(loc) : null);
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var dist = (_transform.GetWorldPosition(xform) - pos.Position).LengthSquared();
            if (dist >= bestDist)
                continue;

            bestDist = dist;
            best = name;
        }

        return best;
    }
    private EntityUid? LocateFugitive()
    {
        var bodies = new List<EntityUid>();
        var query = EntityQueryEnumerator<ImperialFugitiveMindComponent, MindComponent>();
        while (query.MoveNext(out _, out var fugitive, out var mind))
        {
            if (!fugitive.Captured && mind.OwnedEntity is { } body && !_mobs.IsDead(body))
                bodies.Add(body);
        }

        return bodies.Count == 0 ? null : _random.Pick(bodies);
    }

    #endregion

    #region Охотники

    /// <summary>post_equip() отрядов: имя на карте, случайный стиль русских, силы псайкеров, добыча MI13.</summary>
    public void EquipHunter(EntityUid body, ImperialHunterPack pack)
    {
        switch (pack)
        {
            case ImperialHunterPack.Russian:
                if (_random.Prob(0.8f))
                    Replace(body, "jumpsuit", _random.Pick(RussianUniforms));
                if (_random.Prob(0.5f))
                    Replace(body, "outerClothing", _random.Pick(RussianSuits));
                if (_random.Prob(0.5f))
                    Replace(body, "head", _random.Pick(RussianHelmets));
                break;
            case ImperialHunterPack.Psyker:
                if (_inventory.TryGetSlotEntity(body, "eyes", out var eyes) && MetaData(eyes.Value).EntityPrototype?.ID == "ImperialClothingEyesTrickBlindfold")
                {
                    foreach (var action in PsykerActions)
                    {
                        _actions.AddAction(body, action);
                    }
                }
                break;
            case ImperialHunterPack.Mi13:
                foreach (var group in Mi13Loot)
                {
                    var item = Spawn(PickWeight(group), Transform(body).Coordinates);
                    if (!_inventory.TryGetSlotEntity(body, "back", out var bag) || !_containers.TryGetContainer(bag.Value, "storagebase", out var storage)
                        || !_containers.Insert(item, storage))
                        _hands.TryPickupAnyHand(body, item);
                }

                // implants = list(/obj/item/implant/explosive)
                _implants.AddImplant(body, "MicroBombImplant");
                break;
        }

        if (_inventory.TryGetSlotEntity(body, "id", out var id) && TryComp<IdCardComponent>(id, out var card))
            _idCard.TryChangeFullName(id.Value, Name(body), card);
    }

    private EntProtoId PickWeight((EntProtoId Id, int Weight)[] group)
    {
        var roll = _random.Next(group.Sum(g => g.Weight));
        foreach (var (id, weight) in group)
        {
            if (roll < weight)
                return id;
            roll -= weight;
        }

        return group[0].Id;
    }

    /// <summary>Заменить вещь в слоте, сохранив то, что держится на ней (карманы, карта, пояс, хранилище костюма).</summary>
    private void Replace(EntityUid body, string slot, EntProtoId proto)
    {
        var dependent = slot switch
        {
            "jumpsuit" => new[] { "pocket1", "pocket2", "id", "belt" },
            "outerClothing" => new[] { "suitstorage" },
            _ => Array.Empty<string>(),
        };

        var kept = new List<(string Slot, EntityUid Item)>();
        foreach (var dep in dependent)
        {
            if (_inventory.TryGetSlotEntity(body, dep, out var depItem) && _inventory.TryUnequip(body, dep, true, true))
                kept.Add((dep, depItem.Value));
        }

        if (_inventory.TryGetSlotEntity(body, slot, out var old))
        {
            _inventory.TryUnequip(body, slot, true, true);
            QueueDel(old.Value);
        }

        var item = Spawn(proto, Transform(body).Coordinates);
        if (!_inventory.TryEquip(body, item, slot, true, true))
            QueueDel(item);

        foreach (var (depSlot, depItem) in kept)
        {
            if (!_inventory.TryEquip(body, depItem, depSlot, true, true))
                _hands.TryPickupAnyHand(body, depItem);
        }
    }

    #endregion

    #region Псайкеры

    /// <summary>pointed/psychic_projection: 10 секунд цель в ужасе стреляет из того, что держит.</summary>
    private void OnProjection(Entity<HumanoidProfileComponent> ent, ref ImperialPsychicProjectionActionEvent args)
    {
        if (args.Handled || HasComp<ImperialPsychicProjectedComponent>(args.Target))
            return;

        args.Handled = true;
        var projected = EnsureComp<ImperialPsychicProjectedComponent>(args.Target);
        projected.Until = _timing.CurTime + TimeSpan.FromSeconds(10);
        _popup.PopupEntity(Loc.GetString("imperial-psyker-projection-victim"), args.Target, args.Target, Content.Shared.Popups.PopupType.LargeCaution);
        _chat.TryEmoteWithChat(args.Target, "Scream");
    }

    /// <summary>charged/psychic_booster: пальцы на спуске становятся сильнее на 10 секунд.</summary>
    private void OnBooster(Entity<HumanoidProfileComponent> ent, ref ImperialPsychicBoosterActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        EnsureComp<ImperialPsychicBoostedComponent>(ent).Until = _timing.CurTime + TimeSpan.FromSeconds(10);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Weapons/Guns/Cock/sf_rifle_cock.ogg"), ent);
        _popup.PopupEntity(Loc.GetString("imperial-psyker-booster-on"), ent, ent);
        RefreshHeldGuns(ent);
    }

    private void RefreshHeldGuns(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (HasComp<GunComponent>(held))
                _guns.RefreshModifiers(held);
        }
    }

    private void OnGunRefresh(Entity<GunComponent> ent, ref GunRefreshModifiersEvent args)
    {
        if (_containers.TryGetContainingContainer((ent, null), out var container)
            && TryComp<ImperialPsychicBoostedComponent>(container.Owner, out var boosted) && _timing.CurTime < boosted.Until)
            args.FireRate *= 2f;
    }

    /// <summary>forcewall/psychic_wall: стена из трёх клеток поперёк взгляда на 10 секунд.</summary>
    private void OnPsychicWall(Entity<HumanoidProfileComponent> ent, ref ImperialPsychicWallActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        var xform = Transform(ent);
        var side = _transform.GetWorldRotation(xform).RotateVec(new Vector2(1, 0));
        var center = _transform.GetMapCoordinates(xform);
        foreach (var offset in new[] { 0f, 1f, -1f })
        {
            Spawn(PsychicWall, center.Offset(side * offset));
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var invisible = EntityQueryEnumerator<ImperialInvisibilityComponent>();
        while (invisible.MoveNext(out var uid, out var comp))
        {
            if (now < comp.Until)
                continue;

            RemComp<ImperialInvisibilityComponent>(uid);
            RemComp<ImperialCamouflagedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("imperial-fugitive-invisibility-off"), uid, uid);
        }

        var projected = EntityQueryEnumerator<ImperialPsychicProjectedComponent, TransformComponent>();
        while (projected.MoveNext(out var uid, out var comp, out var xform))
        {
            if (now >= comp.Until)
            {
                RemCompDeferred<ImperialPsychicProjectedComponent>(uid);
                continue;
            }

            if (now < comp.NextShot)
                continue;

            comp.NextShot = now + TimeSpan.FromSeconds(0.2);
            foreach (var held in _hands.EnumerateHeld(uid))
            {
                if (!TryComp<GunComponent>(held, out var gun))
                    continue;

                // get_ranged_target_turf(owner, dir, 7) ± клетка.
                var dir = _transform.GetWorldRotation(xform).ToWorldVec();
                var target = xform.Coordinates.Offset(dir * 7f + new Vector2(_random.Next(-1, 2), _random.Next(-1, 2)));
                _guns.AttemptShoot(uid, (held, gun), target);
                break;
            }
        }

        var boosted = EntityQueryEnumerator<ImperialPsychicBoostedComponent>();
        while (boosted.MoveNext(out var uid, out var comp))
        {
            if (now < comp.Until)
                continue;

            RemCompDeferred<ImperialPsychicBoostedComponent>(uid);
            _popup.PopupEntity(Loc.GetString("imperial-psyker-booster-off"), uid, uid);
            RefreshHeldGuns(uid);
        }

        // psyker/process(): засасывает беглецов вплотную.
        var captures = EntityQueryEnumerator<ImperialFugitiveCaptureComponent, TransformComponent>();
        while (captures.MoveNext(out var uid, out var capture, out var xform))
        {
            if (!capture.SuckIn)
                continue;

            foreach (var human in _lookup.GetEntitiesInRange<HumanoidProfileComponent>(xform.Coordinates, 1.5f))
            {
                if (!IsWantedFugitive(human))
                    continue;

                _popup.PopupEntity(Loc.GetString("imperial-fugitive-sucked-in", ("victim", human.Owner)), human, Content.Shared.Popups.PopupType.MediumCaution);
                AddPrisoner(uid, human);
            }
        }
    }
}
