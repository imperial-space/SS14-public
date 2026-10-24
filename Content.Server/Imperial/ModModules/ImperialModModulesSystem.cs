using System.Linq;
using Content.Server.Temperature.Systems;
using Content.Shared.Buckle.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Emp;
using Content.Shared.Forensics.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.ModModules;
using Content.Shared.Mobs;
using Content.Shared.Nutrition.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Standing;
using Content.Shared.Storage;
using Content.Shared.Tag;
using Content.Shared.Temperature.Components;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ModModules;

/// <summary>
/// Поведение модулей МОДа (modules_general / modules_service / modules_supply / modules_security).
/// </summary>
public sealed class ImperialModModulesSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ImperialModSuitSystem _suit = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly TagSystem _tag = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;

    private static readonly ProtoId<TagPrototype> OreTag = "Ore";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModSoundModuleComponent, ImperialModModuleUsedEvent>(OnSoundUsed);
        SubscribeLocalEvent<ImperialModGpsModuleComponent, ImperialModModuleUsedEvent>(OnGpsUsed);
        SubscribeLocalEvent<ImperialModThermalRegulatorComponent, ImperialModModuleProcessEvent>(OnThermalProcess);
        SubscribeLocalEvent<ImperialModJointTorsionComponent, ImperialModModuleProcessEvent>(OnTorsionProcess);
        SubscribeLocalEvent<ImperialModJointTorsionComponent, ImperialModModuleToggledEvent>(OnTorsionToggled);

        SubscribeLocalEvent<ImperialModDeviceRefillComponent, ImperialModModuleProcessEvent>(OnRefillProcess);
        SubscribeLocalEvent<ImperialModStorageModuleComponent, ImperialModModuleInstalledEvent>(OnStorageInstalled);
        SubscribeLocalEvent<ImperialModHolsterComponent, ImperialModModuleUsedEvent>(OnHolsterUsed);
        SubscribeLocalEvent<ImperialModHolsterComponent, ImperialModModuleInstalledEvent>(OnHolsterInstalled);
        SubscribeLocalEvent<ImperialModOreBagComponent, ImperialModModuleUsedEvent>(OnOreBagUsed);
        SubscribeLocalEvent<ImperialModOreBagComponent, ImperialModModuleInstalledEvent>(OnOreBagInstalled);
        SubscribeLocalEvent<ImperialModDnaLockComponent, ImperialModModuleUsedEvent>(OnDnaLockUsed);
        SubscribeLocalEvent<ImperialModDnaLockComponent, ImperialModModuleInstalledEvent>(OnDnaLockInstalled);
        SubscribeLocalEvent<ImperialModSuitComponent, EmpPulseEvent>(OnSuitEmp);
        SubscribeLocalEvent<ImperialModEmpShieldComponent, ImperialModModuleInstalledEvent>(OnEmpShieldInstalled);
        SubscribeLocalEvent<ImperialModMouthholeComponent, ImperialModModuleToggledEvent>(OnMouthholeToggled);
        SubscribeLocalEvent<ImperialModFlatlineComponent, MobStateChangedEvent>(OnFlatline);
    }

    #region М1: простые модули

    private void OnSoundUsed(Entity<ImperialModSoundModuleComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        _audio.PlayPvs(ent.Comp.Sound, args.Wearer);
        args.Handled = true;
    }

    private void OnGpsUsed(Entity<ImperialModGpsModuleComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        var pos = _transform.GetMapCoordinates(args.Wearer);
        _popup.PopupEntity(Loc.GetString("imperial-mod-gps-coords", ("x", (int) pos.X), ("y", (int) pos.Y)), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    private void OnThermalProcess(Entity<ImperialModThermalRegulatorComponent> ent, ref ImperialModModuleProcessEvent args)
    {
        if (!TryComp<TemperatureComponent>(args.Wearer, out var temp))
            return;

        var change = (ent.Comp.Target - temp.CurrentTemperature) * MathF.Min(1f, ent.Comp.Rate * args.FrameTime);
        _temperature.ForceChangeTemperature(args.Wearer, temp.CurrentTemperature + change, temp);
    }

    private void OnTorsionToggled(Entity<ImperialModJointTorsionComponent> ent, ref ImperialModModuleToggledEvent args)
    {
        ent.Comp.LastPosition = null;
    }

    private void OnTorsionProcess(Entity<ImperialModJointTorsionComponent> ent, ref ImperialModModuleProcessEvent args)
    {
        var pos = _transform.GetWorldPosition(args.Wearer);
        var last = ent.Comp.LastPosition;
        ent.Comp.LastPosition = pos;
        if (last == null)
            return;

        // Только своя ходьба: не лёжа, не пристёгнут, не в полёте после броска.
        if (_standing.IsDown(args.Wearer) || TryComp<BuckleComponent>(args.Wearer, out var buckle) && buckle.Buckled || HasComp<ThrownItemComponent>(args.Wearer))
            return;

        var steps = MathF.Min((pos - last.Value).Length(), 10f * args.FrameTime);
        if (steps >= 1f)
            _suit.AddPower(args.Suit, ent.Comp.PowerPerStep * MathF.Floor(steps));
    }

    #endregion

    #region Раздатчики (mister)

    private void OnRefillProcess(Entity<ImperialModDeviceRefillComponent> ent, ref ImperialModModuleProcessEvent args)
    {
        if (!TryComp<ImperialModModuleComponent>(ent, out var module) || module.DeviceEntity is not { } device)
            return;

        if (!_solution.TryGetSolution(device, ent.Comp.Solution, out var soln, out var solution))
            return;

        var add = MathF.Min((float) solution.AvailableVolume, ent.Comp.Rate * args.FrameTime);
        if (add > 0)
            _solution.TryAddReagent(soln.Value, ent.Comp.Reagent, add);
    }

    #endregion

    #region Хранилище

    private void OnStorageInstalled(Entity<ImperialModStorageModuleComponent> ent, ref ImperialModModuleInstalledEvent args)
    {
        if (!TryComp<StorageComponent>(args.Suit, out var storage))
            return;

        if (args.Installed)
        {
            ent.Comp.OriginalGrid = storage.Grid.ToList();
            var right = storage.Grid.Count == 0 ? 8 : storage.Grid.Max(b => b.Right);
            var top = storage.Grid.Count == 0 ? -1 : storage.Grid.Max(b => b.Top);
            storage.Grid.Add(new Box2i(0, top + 1, right, top + ent.Comp.Rows));
            Dirty(args.Suit, storage);
            return;
        }

        if (ent.Comp.OriginalGrid == null)
            return;

        var oldTop = ent.Comp.OriginalGrid.Count == 0 ? -1 : ent.Comp.OriginalGrid.Max(b => b.Top);
        storage.Grid.Clear();
        storage.Grid.AddRange(ent.Comp.OriginalGrid);
        ent.Comp.OriginalGrid = null;

        // Всё, что лежало в снятых рядах, вываливается.
        var coords = Transform(args.Suit).Coordinates;
        foreach (var (item, location) in storage.StoredItems.ToArray())
        {
            if (location.Position.Y > oldTop)
                _container.RemoveEntity(args.Suit, item, destination: coords);
        }

        Dirty(args.Suit, storage);
    }

    #endregion

    #region Кобура

    private void OnHolsterUsed(Entity<ImperialModHolsterComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        var container = _container.EnsureContainer<ContainerSlot>(ent, ImperialModHolsterComponent.ContainerId);
        if (container.ContainedEntity is not { } holstered)
        {
            if (_hands.GetActiveItem(args.Wearer) is not { } held)
            {
                _popup.PopupEntity(Loc.GetString("imperial-mod-holster-nothing"), args.Wearer, args.Wearer);
                return;
            }

            if (!HasComp<GunComponent>(held))
            {
                _popup.PopupEntity(Loc.GetString("imperial-mod-holster-doesnt-fit"), args.Wearer, args.Wearer);
                return;
            }

            if (!_container.Insert(held, container))
                return;

            _popup.PopupEntity(Loc.GetString("imperial-mod-holster-holstered"), args.Wearer, args.Wearer);
            _audio.PlayPvs(ent.Comp.Sound, args.Wearer);
            args.Handled = true;
            return;
        }

        if (!_hands.TryPickupAnyHand(args.Wearer, holstered))
        {
            _popup.PopupEntity(Loc.GetString("imperial-mod-holster-full"), args.Wearer, args.Wearer);
            return;
        }

        _popup.PopupEntity(Loc.GetString("imperial-mod-holster-drawn"), args.Wearer, args.Wearer);
        _audio.PlayPvs(ent.Comp.Sound, args.Wearer);
        args.Handled = true;
    }

    private void OnHolsterInstalled(Entity<ImperialModHolsterComponent> ent, ref ImperialModModuleInstalledEvent args)
    {
        if (!args.Installed && _container.TryGetContainer(ent, ImperialModHolsterComponent.ContainerId, out var container))
            _container.EmptyContainer(container, destination: Transform(args.Suit).Coordinates);
    }

    #endregion

    #region Рудный мешок

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ImperialModOreBagComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var bag, out var xform))
        {
            if (_timing.CurTime < bag.NextPickup)
                continue;

            bag.NextPickup = _timing.CurTime + TimeSpan.FromSeconds(0.25);
            if (!HasComp<ImperialModSuitComponent>(xform.ParentUid) || _suit.GetWearer(xform.ParentUid) is not { } wearer)
                continue;

            var pos = _transform.GetWorldPosition(wearer);
            if (bag.DropPosition is { } drop)
            {
                if ((pos - drop).Length() < 1f)
                    continue;

                bag.DropPosition = null;
            }

            var container = _container.EnsureContainer<Container>(uid, ImperialModOreBagComponent.ContainerId);
            var picked = false;
            foreach (var ore in _lookup.GetEntitiesInRange(wearer, 0.6f, LookupFlags.Dynamic | LookupFlags.Sundries))
            {
                if (_container.IsEntityInContainer(ore) || !_tag.HasTag(ore, OreTag))
                    continue;

                picked |= _container.Insert(ore, container);
            }

            if (picked)
                _audio.PlayPvs(new Robust.Shared.Audio.SoundCollectionSpecifier("storageRustle"), wearer);
        }
    }

    private void OnOreBagUsed(Entity<ImperialModOreBagComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        if (_container.TryGetContainer(ent, ImperialModOreBagComponent.ContainerId, out var container))
            _container.EmptyContainer(container, destination: Transform(args.Wearer).Coordinates);

        ent.Comp.DropPosition = _transform.GetWorldPosition(args.Wearer);
        args.Handled = true;
    }

    private void OnOreBagInstalled(Entity<ImperialModOreBagComponent> ent, ref ImperialModModuleInstalledEvent args)
    {
        if (!args.Installed && _container.TryGetContainer(ent, ImperialModOreBagComponent.ContainerId, out var container))
            _container.EmptyContainer(container, destination: Transform(args.Suit).Coordinates);
    }

    #endregion

    #region ДНК-замок

    private void OnDnaLockUsed(Entity<ImperialModDnaLockComponent> ent, ref ImperialModModuleUsedEvent args)
    {
        if (!TryComp<ImperialModSuitComponent>(args.Suit, out var suit) || !TryComp<DnaComponent>(args.Wearer, out var dna) || dna.DNA == null)
            return;

        suit.LockedDna = dna.DNA;
        Dirty(args.Suit, suit);
        _popup.PopupEntity(Loc.GetString("imperial-mod-dna-updated"), args.Wearer, args.Wearer);
        args.Handled = true;
    }

    private void OnDnaLockInstalled(Entity<ImperialModDnaLockComponent> ent, ref ImperialModModuleInstalledEvent args)
    {
        if (args.Installed || !TryComp<ImperialModSuitComponent>(args.Suit, out var suit))
            return;

        suit.LockedDna = null;
        Dirty(args.Suit, suit);
    }

    /// <summary>ЭМИ сбивает ДНК-замок (если костюм не защищён ЭМИ-щитом — тогда событие не дойдёт).</summary>
    private void OnSuitEmp(Entity<ImperialModSuitComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.LockedDna == null)
            return;

        ent.Comp.LockedDna = null;
        Dirty(ent);
        args.Affected = true;
    }

    #endregion

    #region ЭМИ-щит

    private void OnEmpShieldInstalled(Entity<ImperialModEmpShieldComponent> ent, ref ImperialModModuleInstalledEvent args)
    {
        foreach (var uid in ent.Comp.Protected)
        {
            if (!TerminatingOrDeleted(uid))
                RemComp<ImperialModEmpProtectedComponent>(uid);
        }

        ent.Comp.Protected.Clear();
        if (!args.Installed || !TryComp<ImperialModSuitComponent>(args.Suit, out var suit))
            return;

        var targets = new List<EntityUid> { args.Suit };
        targets.AddRange(suit.Modules.ContainedEntities);
        if (_cell.TryGetBatteryFromSlot(args.Suit, out var battery))
            targets.Add(battery.Value.Owner);

        foreach (var uid in targets)
        {
            if (HasComp<ImperialModEmpProtectedComponent>(uid))
                continue;

            AddComp<ImperialModEmpProtectedComponent>(uid);
            ent.Comp.Protected.Add(uid);
        }
    }

    #endregion

    private void OnFlatline(Entity<ImperialModFlatlineComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState == MobState.Dead)
            _audio.PlayPvs(ent.Comp.Sound, ent);
    }

    #region Раструб для еды

    private void OnMouthholeToggled(Entity<ImperialModMouthholeComponent> ent, ref ImperialModModuleToggledEvent args)
    {
        if (args.Active)
        {
            if (_suit.GetPart(args.Suit, ImperialModPart.Head) is { } helmet && RemComp<IngestionBlockerComponent>(helmet))
                ent.Comp.Helmet = helmet;
            return;
        }

        if (ent.Comp.Helmet is { } old && !TerminatingOrDeleted(old))
            EnsureComp<IngestionBlockerComponent>(old);

        ent.Comp.Helmet = null;
    }

    #endregion
}
