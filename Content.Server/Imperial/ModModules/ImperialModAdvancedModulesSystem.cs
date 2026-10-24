using System.Linq;
using System.Numerics;
using Content.Server.Materials;
using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Foldable;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.ModModules;
using Content.Shared.Implants.Components;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Maps;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Storage.Components;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Content.Shared.Throwing;
using Content.Shared.Trigger;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ModModules;

/// <summary>
/// Модули МОДа этапа М3: применение по цели, ядра аномалий, захват, сонар, невидимость, путеискатель и т. п.
/// </summary>
public sealed class ImperialModAdvancedModulesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ImperialModSuitSystem _suit = default!;
    [Dependency] private readonly ImperialModAdvancedEffectsSystem _effects = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly TurfSystem _turf = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly SharedEntityStorageSystem _entityStorage = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly MaterialStorageSystem _materials = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Content.Shared.Trigger.Systems.TriggerSystem _trigger = default!;

    private static readonly SoundSpecifier SparkSound = new SoundCollectionSpecifier("sparks");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModAnomalyLockComponent, InteractUsingEvent>(OnCoreInteract);
        SubscribeLocalEvent<ImperialModAnomalyLockComponent, GetVerbsEvent<AlternativeVerb>>(OnCoreVerbs);
        SubscribeLocalEvent<ImperialModAnomalyLockComponent, ExaminedEvent>(OnCoreExamined);
        SubscribeLocalEvent<ImperialModAnomalyLockComponent, ImperialModModuleActivateAttemptEvent>(OnCoreAttempt);

        SubscribeLocalEvent<ImperialModTeleporterComponent, ImperialModModuleTargetUsedEvent>(OnTeleportUsed);
        SubscribeLocalEvent<ImperialModTeleporterComponent, ImperialModTeleportDoAfterEvent>(OnTeleportDoAfter);
        SubscribeLocalEvent<ImperialModKinesisComponent, ImperialModModuleTargetUsedEvent>(OnKinesisUsed);
        SubscribeLocalEvent<ImperialModClampComponent, ImperialModModuleTargetUsedEvent>(OnClampUsed);
        SubscribeLocalEvent<ImperialModClampComponent, ImperialModClampDoAfterEvent>(OnClampDoAfter);
        SubscribeLocalEvent<ImperialModClampComponent, ImperialModModuleInstalledEvent>(OnClampInstalled);
        SubscribeLocalEvent<ImperialModCaptureComponent, ImperialModModuleTargetUsedEvent>(OnCaptureUsed);
        SubscribeLocalEvent<ImperialModCaptureComponent, ImperialModCaptureDoAfterEvent>(OnCaptureDoAfter);
        SubscribeLocalEvent<ImperialModMicrowaveBeamComponent, ImperialModModuleTargetUsedEvent>(OnMicrowaveUsed);

        SubscribeLocalEvent<ImperialModDispenserComponent, ImperialModModuleUsedEvent>(OnDispenserUsed);
        SubscribeLocalEvent<ImperialMirageGrenadeComponent, TriggerEvent>(OnMirageTrigger);

        SubscribeLocalEvent<ImperialModRecyclerComponent, ImperialModModuleProcessEvent>(OnRecyclerProcess);
        SubscribeLocalEvent<ImperialModRecyclerComponent, ImperialModModuleToggledEvent>(OnRecyclerToggled);

        SubscribeLocalEvent<ImperialModMagHarnessComponent, DidUnequipHandEvent>(OnHarnessUnequip);

        SubscribeLocalEvent<ImperialModStealthModuleComponent, ImperialModModuleToggledEvent>(OnStealthToggled);
        SubscribeLocalEvent<ImperialModCloakComponent, DamageChangedEvent>(OnCloakDamaged);

        SubscribeLocalEvent<ImperialModShootingModuleComponent, ImperialModModuleUsedEvent>(OnShootingUsed);
        SubscribeLocalEvent<ImperialModShootingModuleComponent, ImperialModModuleToggledEvent>(OnShootingToggled);
        SubscribeLocalEvent<ImperialModShootingModuleComponent, ExaminedEvent>((_, _, args) => args.PushMarkup(Loc.GetString("imperial-mod-shooting-help")));

        SubscribeLocalEvent<ImperialModSonarComponent, ImperialModModuleUsedEvent>(OnSonarUsed);

        SubscribeLocalEvent<ImperialModPathfinderComponent, ImperialModModuleUsedEvent>(OnPathfinderUsed);
        SubscribeLocalEvent<ImperialModPathfinderImplantComponent, ImperialModPathfinderSummonEvent>(OnPathfinderSummon);
    }

    /// <summary>Костюм и носитель модуля, если модуль стоит в надетом костюме.</summary>
    private bool TryGetWorn(EntityUid module, out Entity<ImperialModSuitComponent> suit, out EntityUid wearer)
    {
        suit = default;
        wearer = default;
        var parent = Transform(module).ParentUid;
        if (!TryComp<ImperialModSuitComponent>(parent, out var suitComp) || !suitComp.Modules.Contains(module))
            return false;

        suit = (parent, suitComp);
        if (_suit.GetWearer(parent) is not { } w)
            return false;

        wearer = w;
        return true;
    }

    #region Ядра аномалий

    private void OnCoreInteract(Entity<ImperialModAnomalyLockComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || MetaData(args.Used).EntityPrototype is not { } proto)
            return;

        if (!ent.Comp.Cores.Contains(proto.ID))
        {
            if (proto.ID.StartsWith("AnomalyCore"))
            {
                args.Handled = true;
                _popup.PopupEntity(Loc.GetString("imperial-mod-core-wrong", ("core", CoreNames(ent.Comp))), ent, args.User);
            }

            return;
        }

        args.Handled = true;
        var slot = _container.EnsureContainer<ContainerSlot>(ent, ImperialModAnomalyLockComponent.ContainerId);
        if (slot.ContainedEntity != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-core-occupied"), ent, args.User);
            return;
        }

        if (_container.Insert(args.Used, slot))
            _popup.PopupEntity(Loc.GetString("imperial-mod-core-inserted"), ent, args.User);
    }

    private void OnCoreVerbs(Entity<ImperialModAnomalyLockComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !ent.Comp.Removable
            || !_container.TryGetContainer(ent, ImperialModAnomalyLockComponent.ContainerId, out var slot) || slot.ContainedEntities.Count == 0)
            return;

        var user = args.User;
        var core = slot.ContainedEntities[0];
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("imperial-mod-core-remove"),
            Act = () =>
            {
                _container.Remove(core, slot);
                _hands.PickupOrDrop(user, core);
            },
        });
    }

    private void OnCoreExamined(Entity<ImperialModAnomalyLockComponent> ent, ref ExaminedEvent args)
    {
        var has = _container.TryGetContainer(ent, ImperialModAnomalyLockComponent.ContainerId, out var slot) && slot.ContainedEntities.Count > 0;
        args.PushMarkup(Loc.GetString(has ? "imperial-mod-core-examine-present" : "imperial-mod-core-examine-missing"));
        if (!has)
            args.PushMarkup(Loc.GetString("imperial-mod-core-needed", ("core", CoreNames(ent.Comp))));
    }

    private void OnCoreAttempt(Entity<ImperialModAnomalyLockComponent> ent, ref ImperialModModuleActivateAttemptEvent args)
    {
        if (_container.TryGetContainer(ent, ImperialModAnomalyLockComponent.ContainerId, out var slot) && slot.ContainedEntities.Count > 0)
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("imperial-mod-core-missing"), args.Wearer, args.Wearer);
    }

    private string CoreNames(ImperialModAnomalyLockComponent comp)
    {
        return string.Join(", ", comp.Cores.Select(c => _proto.TryIndex<EntityPrototype>(c, out var p) ? p.Name : c.ToString()));
    }

    #endregion

    #region Телепорт

    private void OnTeleportUsed(Entity<ImperialModTeleporterComponent> ent, ref ImperialModModuleTargetUsedEvent args)
    {
        if (!_transform.InRange(args.Target, Transform(args.Wearer).Coordinates, ent.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-too-far"), args.Wearer, args.Wearer);
            return;
        }

        if (!_turf.TryGetTileRef(args.Target, out var tile) || _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable)
            || !_examine.InRangeUnOccluded(args.Wearer, args.Target, ent.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-teleport-blocked"), args.Wearer, args.Wearer);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.Wearer, ent.Comp.Delay,
            new ImperialModTeleportDoAfterEvent(GetNetCoordinates(args.Target)), ent, used: ent)
        {
            BreakOnDamage = true,
        };
        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnTeleportDoAfter(Entity<ImperialModTeleporterComponent> ent, ref ImperialModTeleportDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var target = GetCoordinates(args.Target);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Effects/teleport_departure.ogg"), Transform(args.User).Coordinates);
        _transform.SetCoordinates(args.User, target);
        _transform.AttachToGridOrMap(args.User);
        _audio.PlayPvs(ent.Comp.Sound, args.User);
    }

    #endregion

    #region Кинезис

    private void OnKinesisUsed(Entity<ImperialModKinesisComponent> ent, ref ImperialModModuleTargetUsedEvent args)
    {
        if (args.TargetEntity is not { } target || target == args.Wearer || !HasComp<ItemComponent>(target)
            || Transform(target).Anchored || _container.IsEntityInContainer(target))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-kinesis-invalid"), args.Wearer, args.Wearer);
            return;
        }

        if (!_examine.InRangeUnOccluded(args.Wearer, target, ent.Comp.Range))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-too-far"), args.Wearer, args.Wearer);
            return;
        }

        _throwing.TryThrow(target, Transform(args.Wearer).Coordinates, ent.Comp.ThrowSpeed, args.Wearer, recoil: false);
        args.Handled = true;
    }

    #endregion

    #region Зажим ящиков

    /// <summary>Правила грейфера меха: не статичное, не настенное, не моб и не закреплённое.</summary>
    private bool CanGrab(EntityUid uid, EntityUid wearer)
    {
        if (uid == wearer || HasComp<MobStateComponent>(uid) || HasComp<Content.Shared.Wall.WallMountComponent>(uid) || Transform(uid).Anchored)
            return false;

        return !TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType != Robust.Shared.Physics.BodyType.Static;
    }

    private void OnClampUsed(Entity<ImperialModClampComponent> ent, ref ImperialModModuleTargetUsedEvent args)
    {
        var container = _container.EnsureContainer<Container>(ent, ImperialModClampComponent.ContainerId);
        if (args.TargetEntity is { } target && !_container.IsEntityInContainer(target) && CanGrab(target, args.Wearer))
        {
            if (!_interaction.InRangeUnobstructed(args.Wearer, target))
                return;

            if (container.ContainedEntities.Count >= ent.Comp.MaxCrates)
            {
                _popup.PopupEntity(Loc.GetString("imperial-mod-clamp-full"), args.Wearer, args.Wearer);
                return;
            }

            if (ContainsMob(target))
            {
                _popup.PopupEntity(Loc.GetString("imperial-mod-clamp-invalid"), args.Wearer, args.Wearer);
                return;
            }

            if (!_suit.DrainPower(args.Suit, ent.Comp.GrabPower))
            {
                _popup.PopupEntity(Loc.GetString("imperial-mod-no-charge"), args.Wearer, args.Wearer);
                return;
            }

            ent.Comp.AudioStream = _audio.PlayPvs(ent.Comp.GrabSound, args.Wearer)?.Entity;
            var doAfter = new DoAfterArgs(EntityManager, args.Wearer, ent.Comp.LoadTime, new ImperialModClampDoAfterEvent(), ent, target: target, used: ent)
            {
                BreakOnMove = true,
                BreakOnDamage = true,
            };
            args.Handled = _doAfter.TryStartDoAfter(doAfter);
            return;
        }

        if (container.ContainedEntities.Count == 0)
            return;

        if (!_interaction.InRangeUnobstructed(args.Wearer, args.Target)
            || !_turf.TryGetTileRef(args.Target, out var tile) || _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-clamp-blocked"), args.Wearer, args.Wearer);
            return;
        }

        var crate = container.ContainedEntities[^1];
        _container.Remove(crate, container, destination: _turf.GetTileCenter(tile.Value));
        _popup.PopupEntity(Loc.GetString("imperial-mod-clamp-dropped", ("crate", crate)), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    private bool ContainsMob(EntityUid crate)
    {
        return TryComp<EntityStorageComponent>(crate, out var storage)
               && storage.Contents.ContainedEntities.Any(HasComp<MobStateComponent>);
    }

    private void OnClampDoAfter(Entity<ImperialModClampComponent> ent, ref ImperialModClampDoAfterEvent args)
    {
        if (args.Cancelled)
            ent.Comp.AudioStream = _audio.Stop(ent.Comp.AudioStream);

        if (args.Cancelled || args.Handled || args.Target is not { } target || TerminatingOrDeleted(target))
            return;

        args.Handled = true;
        var container = _container.EnsureContainer<Container>(ent, ImperialModClampComponent.ContainerId);
        if (container.ContainedEntities.Count >= ent.Comp.MaxCrates || ContainsMob(target))
            return;

        if (_container.Insert(target, container))
            _popup.PopupEntity(Loc.GetString("imperial-mod-clamp-loaded", ("crate", target)), args.User, args.User);
    }

    private void OnClampInstalled(Entity<ImperialModClampComponent> ent, ref ImperialModModuleInstalledEvent args)
    {
        if (!args.Installed && _container.TryGetContainer(ent, ImperialModClampComponent.ContainerId, out var container))
            _container.EmptyContainer(container, destination: Transform(args.Suit).Coordinates);
    }

    #endregion

    #region Захват преступника / перевозка пациента

    private void OnCaptureUsed(Entity<ImperialModCaptureComponent> ent, ref ImperialModModuleTargetUsedEvent args)
    {
        if (ent.Comp.LinkedBag is { } bag && args.TargetEntity == bag)
        {
            var packup = new DoAfterArgs(EntityManager, args.Wearer, ent.Comp.PackupTime,
                new ImperialModCaptureDoAfterEvent(GetNetCoordinates(args.Target), true), ent, target: bag, used: ent)
            {
                BreakOnMove = true,
            };
            args.Handled = _doAfter.TryStartDoAfter(packup);
            return;
        }

        if (ent.Comp.LinkedBag != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-capture-linked"), args.Wearer, args.Wearer);
            return;
        }

        if (!_interaction.InRangeUnobstructed(args.Wearer, args.Target)
            || !_turf.TryGetTileRef(args.Target, out var tile) || _turf.IsTileBlocked(tile.Value, CollisionGroup.Impassable))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.Wearer, ent.Comp.CaptureTime,
            new ImperialModCaptureDoAfterEvent(GetNetCoordinates(_turf.GetTileCenter(tile.Value)), false), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };
        args.Handled = _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnCaptureDoAfter(Entity<ImperialModCaptureComponent> ent, ref ImperialModCaptureDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        if (args.Packup)
        {
            Packup(ent);
            return;
        }

        if (ent.Comp.LinkedBag != null)
            return;

        var bag = Spawn(ent.Comp.Bag, GetCoordinates(args.Target));
        // Мешок из твёрдого света нельзя сложить и унести.
        RemComp<FoldableComponent>(bag);
        RemComp<ItemComponent>(bag);
        EnsureComp<ImperialModHardlightBagComponent>(bag).Module = ent;
        ent.Comp.LinkedBag = bag;
        ent.Comp.NextDrain = _timing.CurTime + TimeSpan.FromSeconds(1);
        _entityStorage.CloseStorage(bag);
    }

    private void Packup(Entity<ImperialModCaptureComponent> ent)
    {
        if (ent.Comp.LinkedBag is not { } bag)
            return;

        ent.Comp.LinkedBag = null;
        if (TerminatingOrDeleted(bag))
            return;

        _entityStorage.OpenStorage(bag);
        if (TryComp<EntityStorageComponent>(bag, out var storage))
            _container.EmptyContainer(storage.Contents, destination: Transform(bag).Coordinates);

        _audio.PlayPvs(SparkSound, Transform(bag).Coordinates);
        QueueDel(bag);
    }

    #endregion

    #region Микроволновый луч

    private void OnMicrowaveUsed(Entity<ImperialModMicrowaveBeamComponent> ent, ref ImperialModModuleTargetUsedEvent args)
    {
        if (args.TargetEntity is not { } target || !HasComp<ItemComponent>(target) || _container.IsEntityInContainer(target)
            || !_examine.InRangeUnOccluded(args.Wearer, target, ent.Comp.Range))
            return;

        _audio.PlayPvs(SparkSound, args.Wearer);
        _audio.PlayPvs(SparkSound, target);

        var heated = false;
        if (TryComp<SolutionContainerManagerComponent>(target, out var manager))
        {
            foreach (var (_, soln) in _solution.EnumerateSolutions((target, manager)))
            {
                _solution.AddThermalEnergy(soln, ent.Comp.Heat);
                heated = true;
            }
        }

        _popup.PopupEntity(Loc.GetString(heated ? "imperial-mod-microwave-success" : "imperial-mod-microwave-fail", ("target", target)), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    #endregion

    #region Мираж

    private void OnDispenserUsed(Entity<ImperialModDispenserComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        var item = Spawn(ent.Comp.Item, Transform(args.Wearer).Coordinates);
        if (TryComp<ImperialMirageGrenadeComponent>(item, out var mirage))
            mirage.Owner = args.Wearer;

        _hands.PickupOrDrop(args.Wearer, item);

        // arm_grenade(): граната выдаётся уже взведённой.
        if (mirage != null)
            _trigger.ActivateTimerTrigger(item, args.Wearer);
        _popup.PopupEntity(Loc.GetString("imperial-mod-dispenser-dispensed", ("item", item)), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    private void OnMirageTrigger(Entity<ImperialMirageGrenadeComponent> ent, ref TriggerEvent args)
    {
        if (args.Key != "timer")
            return;

        var source = ent.Comp.Owner ?? args.User;
        if (source == null || TerminatingOrDeleted(source))
            return;

        var coords = Transform(ent).Coordinates;
        for (var i = 0; i < ent.Comp.Count; i++)
        {
            var holo = Spawn(ent.Comp.Hologram, coords);
            var comp = EnsureComp<ImperialMirageHologramComponent>(holo);
            comp.Source = source;
            _meta.SetEntityName(holo, Name(source.Value));
            _meta.SetEntityDescription(holo, Description(source.Value));
            Dirty(holo, comp);
        }
    }

    #endregion

    #region Утилизатор

    private void OnRecyclerProcess(Entity<ImperialModRecyclerComponent> ent, ref ImperialModModuleProcessEvent args)
    {
        foreach (var item in _lookup.GetEntitiesInRange(args.Wearer, 0.6f, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (_container.IsEntityInContainer(item) || !ent.Comp.Tags.Any(t => _tag.HasTag(item, t)))
                continue;

            if (TryComp<PhysicalCompositionComponent>(item, out var composition))
            {
                foreach (var (material, amount) in composition.MaterialComposition)
                    ent.Comp.Materials[material] = ent.Comp.Materials.GetValueOrDefault(material) + amount;
            }

            QueueDel(item);
        }
    }

    private void OnRecyclerToggled(Entity<ImperialModRecyclerComponent> ent, ref ImperialModModuleToggledEvent args)
    {
        if (args.Active || args.Wearer is not { } wearer || ent.Comp.Materials.Count == 0)
            return;

        var coords = Transform(wearer).Coordinates;
        foreach (var (material, amount) in ent.Comp.Materials.ToArray())
        {
            _materials.SpawnMultipleFromMaterial(amount, material, coords, out var overflow);
            if (overflow > 0)
                ent.Comp.Materials[material] = overflow;
            else
                ent.Comp.Materials.Remove(material);
        }
    }

    #endregion

    #region Магнитная сбруя

    private void OnHarnessUnequip(Entity<ImperialModMagHarnessComponent> ent, ref DidUnequipHandEvent args)
    {
        if (HasComp<GunComponent>(args.Unequipped))
            ent.Comp.Pending.Add((args.Unequipped, _timing.CurTime + TimeSpan.FromSeconds(1)));
    }

    private void UpdateHarness()
    {
        var query = EntityQueryEnumerator<ImperialModMagHarnessComponent>();
        while (query.MoveNext(out var uid, out var harness))
        {
            for (var i = harness.Pending.Count - 1; i >= 0; i--)
            {
                var (gun, at) = harness.Pending[i];
                if (_timing.CurTime < at)
                    continue;

                harness.Pending.RemoveAt(i);
                if (TerminatingOrDeleted(gun) || _container.IsEntityInContainer(gun)
                    || !_transform.InRange(Transform(gun).Coordinates, Transform(uid).Coordinates, 3f))
                    continue;

                if (_inventory.TryGetSlotEntity(uid, "suitstorage", out _)
                    || !_inventory.TryEquip(uid, gun, "suitstorage", silent: true, force: true))
                    continue;

                _popup.PopupEntity(Loc.GetString("imperial-mod-harness-snap", ("gun", gun)), uid, uid);
            }
        }
    }

    #endregion

    #region Гаситель снарядов

    private void UpdateDampeners()
    {
        var query = EntityQueryEnumerator<ImperialModDampenerComponent>();
        while (query.MoveNext(out var uid, out var dampener))
        {
            foreach (var projectile in _lookup.GetEntitiesInRange<ProjectileComponent>(Transform(uid).Coordinates, dampener.Radius))
            {
                if (projectile.Comp.Shooter == uid || HasComp<ImperialModDampenedComponent>(projectile))
                    continue;

                AddComp<ImperialModDampenedComponent>(projectile);
                projectile.Comp.Damage *= dampener.DamageMultiplier;
                if (TryComp<PhysicsComponent>(projectile, out var body))
                    _physics.SetLinearVelocity(projectile, body.LinearVelocity * dampener.SpeedMultiplier, body: body);
            }
        }
    }

    #endregion

    #region Невидимость

    private void OnStealthToggled(Entity<ImperialModStealthModuleComponent> ent, ref ImperialModModuleToggledEvent args)
    {
        if (args.Wearer is not { } wearer)
            return;

        if (args.Active)
        {
            EnsureComp<ImperialModCloakComponent>(wearer).Module = ent;
            if (TryComp<ImperialModModuleComponent>(ent, out var module))
                _suit.DrainPower(args.Suit, module.UsePower);
            return;
        }

        RemComp<ImperialModCloakComponent>(wearer);
    }

    private void OnCloakDamaged(Entity<ImperialModCloakComponent> ent, ref DamageChangedEvent args)
    {
        if (!args.DamageIncreased || !TryComp<ImperialModModuleComponent>(ent.Comp.Module, out var module)
            || !TryGetWorn(ent.Comp.Module, out var suit, out _))
            return;

        _popup.PopupEntity(Loc.GetString("imperial-mod-stealth-broken"), ent, ent, PopupType.MediumCaution);
        _audio.PlayPvs(SparkSound, ent);
        _suit.Deactivate(suit, (ent.Comp.Module, module), null, false);
        _suit.StartCooldown((ent.Comp.Module, module));
    }

    #endregion

    #region Помощник стрелка

    private void OnShootingUsed(Entity<ImperialModShootingModuleComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        ent.Comp.Mode = ent.Comp.Mode switch
        {
            ImperialModShootingMode.Off => ImperialModShootingMode.Stormtrooper,
            ImperialModShootingMode.Stormtrooper => ImperialModShootingMode.Sharpshooter,
            _ => ImperialModShootingMode.Off,
        };

        ApplyShootingMode(ent, args.Wearer);
        _popup.PopupEntity(Loc.GetString($"imperial-mod-shooting-{ent.Comp.Mode.ToString().ToLowerInvariant()}"), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    private void OnShootingToggled(Entity<ImperialModShootingModuleComponent> ent, ref ImperialModModuleToggledEvent args)
    {
        if (args.Wearer is not { } wearer)
            return;

        if (args.Active)
        {
            ApplyShootingMode(ent, wearer);
            return;
        }

        RemComp<ImperialModShootingAssistantComponent>(wearer);
    }

    private void ApplyShootingMode(Entity<ImperialModShootingModuleComponent> ent, EntityUid wearer)
    {
        if (TryComp<ImperialModModuleComponent>(ent, out var module))
            module.IdlePower = ent.Comp.Mode switch
            {
                ImperialModShootingMode.Stormtrooper => 0.4f,
                ImperialModShootingMode.Sharpshooter => 0.6f,
                _ => 0f,
            };

        if (ent.Comp.Mode == ImperialModShootingMode.Off)
        {
            RemComp<ImperialModShootingAssistantComponent>(wearer);
            return;
        }

        var assistant = EnsureComp<ImperialModShootingAssistantComponent>(wearer);
        _effects.SetMode((wearer, assistant), ent.Comp.Mode);
    }

    #endregion

    #region Сонар

    private void UpdateSonars()
    {
        var query = EntityQueryEnumerator<ImperialModSonarComponent, ImperialModModuleComponent>();
        while (query.MoveNext(out var uid, out var sonar, out var module))
        {
            if (_timing.CurTime < sonar.NextScan)
                continue;

            sonar.NextScan = _timing.CurTime + sonar.ScanInterval;
            if (_timing.CurTime < module.NextUse || !TryGetWorn(uid, out var suit, out var wearer)
                || !_suit.IsActive(suit) || !_suit.HasParts(suit, module.RequiredParts))
                continue;

            sonar.Slice = (sonar.Slice + 1) % sonar.Slices;
            Ping(sonar, wearer, sonar.Slice);
        }
    }

    /// <summary>Показать носителю живых существ в секторе (или во всех секторах при slice = null).</summary>
    private int Ping(ImperialModSonarComponent sonar, EntityUid wearer, int? slice)
    {
        var origin = _transform.GetWorldPosition(wearer);
        var found = 0;
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(wearer).Coordinates, sonar.Range))
        {
            if (mob.Owner == wearer || _mobState.IsDead(mob, mob.Comp))
                continue;

            if (slice != null)
            {
                var dir = _transform.GetWorldPosition(mob) - origin;
                var angle = (Angle.FromWorldVec(dir).Degrees + 360) % 360;
                if ((int) (angle / (360f / sonar.Slices)) != slice)
                    continue;
            }

            found++;
            _popup.PopupCoordinates("◉", Transform(mob).Coordinates, wearer, PopupType.Small);
        }

        return found;
    }

    private void OnSonarUsed(Entity<ImperialModSonarComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        var found = Ping(ent.Comp, args.Wearer, null);
        _audio.PlayPvs(ent.Comp.Sound, args.Wearer, AudioParams.Default.WithVolume(4f).WithMaxDistance(ent.Comp.Range));
        _popup.PopupEntity(Loc.GetString("imperial-mod-sonar-found", ("count", found)), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    #endregion

    #region Путеискатель

    private void OnPathfinderUsed(Entity<ImperialModPathfinderComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        var implanter = Spawn(ent.Comp.Implanter, Transform(args.Wearer).Coordinates);
        if (TryComp<ImplanterComponent>(implanter, out var comp) && comp.ImplanterSlot.ContainerSlot?.ContainedEntity is { } implant
            && TryComp<ImperialModPathfinderImplantComponent>(implant, out var pathfinder))
            pathfinder.Suit = args.Suit;

        _hands.PickupOrDrop(args.Wearer, implanter);
        _popup.PopupEntity(Loc.GetString("imperial-mod-pathfinder-implanter"), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    private void OnPathfinderSummon(Entity<ImperialModPathfinderImplantComponent> ent, ref ImperialModPathfinderSummonEvent args)
    {
        args.Handled = true;
        if (ent.Comp.Suit is not { } suit || TerminatingOrDeleted(suit))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-pathfinder-no-suit"), args.Performer, args.Performer);
            return;
        }

        if (_suit.GetWearer(suit) != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-pathfinder-worn"), args.Performer, args.Performer);
            return;
        }

        var travel = TryComp<ImperialModSuitComponent>(suit, out var suitComp)
            ? suitComp.Modules.ContainedEntities.Select(m => CompOrNull<ImperialModPathfinderComponent>(m)?.TravelTime).FirstOrDefault(t => t != null)
            : null;
        ent.Comp.ArriveAt = _timing.CurTime + (travel ?? TimeSpan.FromSeconds(5));
        _popup.PopupEntity(Loc.GetString("imperial-mod-pathfinder-summoned"), args.Performer, args.Performer);
    }

    private void UpdatePathfinders()
    {
        var query = EntityQueryEnumerator<ImperialModPathfinderImplantComponent>();
        while (query.MoveNext(out var uid, out var implant))
        {
            if (implant.ArriveAt is not { } at || _timing.CurTime < at)
                continue;

            implant.ArriveAt = null;
            var owner = Transform(uid).ParentUid;
            if (implant.Suit is not { } suit || TerminatingOrDeleted(suit) || !HasComp<MobStateComponent>(owner))
                continue;

            _audio.PlayPvs(SparkSound, Transform(suit).Coordinates);
            _container.TryRemoveFromContainer(suit, true);
            _transform.SetCoordinates(suit, Transform(owner).Coordinates);
            _transform.AttachToGridOrMap(suit);
            _audio.PlayPvs(SparkSound, suit);

            if (!_inventory.TryGetSlotEntity(owner, "back", out _))
                _inventory.TryEquip(owner, suit, "back", silent: true, force: true);
        }
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateHarness();
        UpdateDampeners();
        UpdateSonars();
        UpdatePathfinders();
        UpdateCaptures();
        UpdateHolograms();
    }

    private void UpdateCaptures()
    {
        var query = EntityQueryEnumerator<ImperialModCaptureComponent>();
        while (query.MoveNext(out var uid, out var capture))
        {
            if (capture.LinkedBag is not { } bag)
                continue;

            if (TerminatingOrDeleted(bag))
            {
                capture.LinkedBag = null;
                continue;
            }

            if (_timing.CurTime < capture.NextDrain)
                continue;

            capture.NextDrain = _timing.CurTime + TimeSpan.FromSeconds(1);
            if (!TryGetWorn(uid, out var suit, out var wearer)
                || !_transform.InRange(Transform(bag).Coordinates, Transform(wearer).Coordinates, capture.MaxRange)
                || !_suit.DrainPower(suit, capture.LinkedPower))
            {
                Packup((uid, capture));
            }
        }
    }

    private void UpdateHolograms()
    {
        var query = EntityQueryEnumerator<ImperialMirageHologramComponent>();
        while (query.MoveNext(out var uid, out var holo))
        {
            if (_timing.CurTime < holo.NextMove)
                continue;

            holo.NextMove = _timing.CurTime + TimeSpan.FromSeconds(_random.NextFloat(0.5f, 1.5f));
            var dir = _random.NextAngle().ToVec() * _random.NextFloat(1.5f, 3.5f);
            _physics.SetLinearVelocity(uid, dir);
        }
    }
}
