using System.Linq;
using Content.Shared.Actions;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.ModModules;
using Content.Shared.Interaction.Components;
using Content.Shared.Inventory.Events;
using Content.Shared.PowerCell;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.ModModules;

/// <summary>
/// Серверная часть МОДа: кнопки модулей, расход энергии, включение и выключение,
/// устройства в руке и наложение эффектов модулей.
/// </summary>
public sealed class ImperialModSuitSystem : SharedImperialModSuitSystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly MetaDataSystem _meta = default!;
    [Dependency] private readonly Content.Shared.Power.EntitySystems.SharedBatterySystem _battery = default!;

    private const string DeviceContainerId = "imperial-mod-device";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImperialModSuitComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImperialModSuitComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ImperialModSuitComponent, EntRemovedFromContainerMessage>(OnRemoved);
        SubscribeLocalEvent<ImperialModSuitComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ImperialModSuitComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ImperialModSuitComponent, ImperialModModuleActionEvent>(OnModuleAction);
        SubscribeLocalEvent<ImperialModSuitComponent, ImperialModModuleTargetActionEvent>(OnModuleTargetAction);
        SubscribeLocalEvent<ImperialModDeviceComponent, ComponentShutdown>(OnDeviceShutdown);
    }

    private void OnMapInit(Entity<ImperialModSuitComponent> ent, ref MapInitEvent args)
    {
        foreach (var proto in ent.Comp.StartingModules)
        {
            var module = Spawn(proto, Transform(ent).Coordinates);
            if (!Container.Insert(module, ent.Comp.Modules))
                Del(module);
        }
    }

    private void OnInserted(Entity<ImperialModSuitComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ImperialModSuitComponent.ContainerId || !TryComp<ImperialModModuleComponent>(args.Entity, out var module))
            return;

        var installed = new ImperialModModuleInstalledEvent(ent, true);
        RaiseLocalEvent(args.Entity, ref installed);

        if (module.ModuleType == ImperialModModuleType.Passive)
            return;

        // Кнопка модуля живёт в контейнере действий блока управления и выдаётся вместе с ним.
        if (!ActionContainer.EnsureAction(ent, ref module.ActionEntity, module.Action) || module.ActionEntity is not { } action)
            return;

        _meta.SetEntityName(action, Name(args.Entity));
        _meta.SetEntityDescription(action, Description(args.Entity));
        Actions.SetEntityIcon(action, args.Entity);
        Dirty(args.Entity, module);

        if (ent.Comp.Wearer is { } wearer)
            Actions.GrantContainedAction(wearer, ent.Owner, action);
    }

    private void OnRemoved(Entity<ImperialModSuitComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ImperialModSuitComponent.ContainerId || !TryComp<ImperialModModuleComponent>(args.Entity, out var module))
            return;

        var moduleEnt = new Entity<ImperialModModuleComponent>(args.Entity, module);
        Deactivate(ent, moduleEnt, null, false);
        RemoveEffects(ent, moduleEnt);

        var removed = new ImperialModModuleInstalledEvent(ent, false);
        RaiseLocalEvent(args.Entity, ref removed);

        if (module.ActionEntity != null)
        {
            if (!TerminatingOrDeleted(ent) && !TerminatingOrDeleted(module.ActionEntity))
                ActionContainer.RemoveAction(module.ActionEntity);
            module.ActionEntity = null;
            Dirty(args.Entity, module);
        }
    }

    private void OnEquipped(Entity<ImperialModSuitComponent> ent, ref GotEquippedEvent args)
    {
        ent.Comp.Wearer = args.Slot == "back" ? args.EquipTarget : null;
        Refresh(ent);
    }

    private void OnUnequipped(Entity<ImperialModSuitComponent> ent, ref GotUnequippedEvent args)
    {
        foreach (var module in ent.Comp.Modules.ContainedEntities)
        {
            if (!TryComp<ImperialModModuleComponent>(module, out var comp))
                continue;

            Deactivate(ent, (module, comp), null, false);
            RemoveEffects(ent, (module, comp));
        }

        ent.Comp.Wearer = null;
    }

    protected override void OnPanelToggled(Entity<ImperialModSuitComponent> ent)
    {
        Refresh(ent);
    }

    #region Кнопка модуля

    private void OnModuleAction(Entity<ImperialModSuitComponent> ent, ref ImperialModModuleActionEvent args)
    {
        if (args.Handled)
            return;

        foreach (var module in ent.Comp.Modules.ContainedEntities)
        {
            if (!TryComp<ImperialModModuleComponent>(module, out var comp) || comp.ActionEntity != args.Action.Owner)
                continue;

            args.Handled = true;
            Select(ent, (module, comp), args.Performer);
            return;
        }
    }

    private void OnModuleTargetAction(Entity<ImperialModSuitComponent> ent, ref ImperialModModuleTargetActionEvent args)
    {
        if (args.Handled)
            return;

        foreach (var module in ent.Comp.Modules.ContainedEntities)
        {
            if (!TryComp<ImperialModModuleComponent>(module, out var comp) || comp.ActionEntity != args.Action.Owner)
                continue;

            args.Handled = true;
            Select(ent, (module, comp), args.Performer, args.Target, args.Entity);
            return;
        }
    }

    /// <summary>on_select(): проверки костюма, затем применение или переключение.</summary>
    public void Select(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid user, EntityCoordinates? target = null, EntityUid? targetEntity = null)
    {
        var wearer = GetWearer(suit);
        if (wearer == null)
        {
            Popup.PopupEntity(Loc.GetString("imperial-mod-not-equipped"), suit, user);
            return;
        }

        if (suit.Comp.LockedDna != null && !IsActive(suit) && GetPart(suit, ImperialModPart.Outer) != null && !suit.Comp.Open)
        {
            Popup.PopupEntity(Loc.GetString("imperial-mod-dna-locked"), suit, user);
            return;
        }

        if (module.Comp.ModuleType == ImperialModModuleType.Passive || !IsActive(suit) && !module.Comp.AllowInactive)
        {
            Popup.PopupEntity(Loc.GetString("imperial-mod-not-active"), suit, user);
            return;
        }

        if (!HasParts(suit, module.Comp.RequiredParts))
        {
            Popup.PopupEntity(Loc.GetString("imperial-mod-parts-inactive"), suit, user);
            return;
        }

        if (module.Comp.ModuleType == ImperialModModuleType.Usable || target != null)
        {
            Use(suit, module, wearer.Value, user, target, targetEntity);
            return;
        }

        if (module.Comp.Active)
            Deactivate(suit, module, user, true);
        else
            Activate(suit, module, wearer.Value, user);
    }

    /// <summary>used(): кулдаун, энергия, событие применения.</summary>
    private void Use(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid wearer, EntityUid user, EntityCoordinates? target, EntityUid? targetEntity)
    {
        if (Timing.CurTime < module.Comp.NextUse)
        {
            Popup.PopupEntity(Loc.GetString("imperial-mod-cooldown"), suit, user);
            return;
        }

        if (!HasPower(suit, module.Comp.UsePower))
        {
            Popup.PopupEntity(Loc.GetString("imperial-mod-no-charge"), suit, user);
            return;
        }

        var attempt = new ImperialModModuleActivateAttemptEvent(suit, wearer);
        RaiseLocalEvent(module, ref attempt);
        if (attempt.Cancelled)
            return;

        if (target != null)
        {
            var targetEv = new ImperialModModuleTargetUsedEvent(suit, wearer, target.Value, targetEntity);
            RaiseLocalEvent(module, ref targetEv);
            if (!targetEv.Handled)
                return;
        }
        else
        {
            var ev = new ImperialModModuleUsedEvent(suit, wearer);
            RaiseLocalEvent(module, ref ev);
            if (!ev.Handled)
                return;
        }

        DrainPower(suit, module.Comp.UsePower);
        StartCooldown(module);
    }

    public void StartCooldown(Entity<ImperialModModuleComponent> module, TimeSpan? cooldown = null)
    {
        var time = cooldown ?? module.Comp.Cooldown;
        module.Comp.NextUse = Timing.CurTime + time;
        if (time > TimeSpan.Zero && module.Comp.ActionEntity != null)
            Actions.SetCooldown(module.Comp.ActionEntity, time);
    }

    /// <summary>activate(): для активных модулей — выдать устройство и снять выбор с другого.</summary>
    public bool Activate(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid wearer, EntityUid? user)
    {
        if (Timing.CurTime < module.Comp.NextUse)
        {
            if (user != null)
                Popup.PopupEntity(Loc.GetString("imperial-mod-cooldown"), suit, user.Value);
            return false;
        }

        if (!HasPower(suit, 0.01f))
        {
            if (user != null)
                Popup.PopupEntity(Loc.GetString("imperial-mod-unpowered"), suit, user.Value);
            return false;
        }

        var attempt = new ImperialModModuleActivateAttemptEvent(suit, wearer);
        RaiseLocalEvent(module, ref attempt);
        if (attempt.Cancelled)
            return false;

        if (module.Comp.ModuleType == ImperialModModuleType.Active)
        {
            foreach (var other in suit.Comp.Modules.ContainedEntities)
            {
                if (other != module.Owner && TryComp<ImperialModModuleComponent>(other, out var otherComp)
                    && otherComp.ModuleType == ImperialModModuleType.Active && otherComp.Active)
                {
                    Deactivate(suit, (other, otherComp), null, false);
                }
            }

            if (module.Comp.Device != null && !ExtendDevice(module, wearer))
            {
                if (user != null)
                    Popup.PopupEntity(Loc.GetString("imperial-mod-cant-extend", ("module", module.Owner)), suit, user.Value);
                return false;
            }
        }

        module.Comp.Active = true;
        Dirty(module);
        if (module.Comp.ActionEntity != null)
            Actions.SetToggled(module.Comp.ActionEntity, true);

        if (user != null)
        {
            var msg = module.Comp.DeviceEntity is { } device
                ? Loc.GetString("imperial-mod-device-extended", ("device", device))
                : Loc.GetString("imperial-mod-module-activated", ("module", module.Owner));
            Popup.PopupEntity(msg, wearer, user.Value);
        }

        Refresh(suit);
        return true;
    }

    /// <summary>deactivate(): убрать устройство, снять эффекты.</summary>
    public void Deactivate(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid? user, bool message)
    {
        if (!module.Comp.Active)
            return;

        module.Comp.Active = false;
        Dirty(module);
        if (module.Comp.ActionEntity != null)
            Actions.SetToggled(module.Comp.ActionEntity, false);

        var device = module.Comp.DeviceEntity;
        RetractDevice(module);

        if (message && user != null)
        {
            var msg = device != null && !TerminatingOrDeleted(device)
                ? Loc.GetString("imperial-mod-device-retracted", ("device", device.Value))
                : Loc.GetString("imperial-mod-module-deactivated", ("module", module.Owner));
            Popup.PopupEntity(msg, user.Value, user.Value);
        }

        Refresh(suit);
    }

    #endregion

    #region Устройства

    private bool ExtendDevice(Entity<ImperialModModuleComponent> module, EntityUid wearer)
    {
        if (module.Comp.DeviceEntity == null || TerminatingOrDeleted(module.Comp.DeviceEntity))
        {
            var container = Container.EnsureContainer<ContainerSlot>(module, DeviceContainerId);
            var spawned = Spawn(module.Comp.Device, Transform(module).Coordinates);
            EnsureComp<ImperialModDeviceComponent>(spawned).Module = module;
            Container.Insert(spawned, container);
            module.Comp.DeviceEntity = spawned;
        }

        var device = module.Comp.DeviceEntity.Value;
        if (!_hands.TryPickupAnyHand(wearer, device))
            return false;

        var unremoveable = EnsureComp<UnremoveableComponent>(device);
        unremoveable.DeleteOnDrop = false;
        Dirty(device, unremoveable);
        return true;
    }

    private void RetractDevice(Entity<ImperialModModuleComponent> module)
    {
        if (module.Comp.DeviceEntity is not { } device || TerminatingOrDeleted(device))
            return;

        RemComp<UnremoveableComponent>(device);
        var container = Container.EnsureContainer<ContainerSlot>(module, DeviceContainerId);
        if (!container.Contains(device))
            Container.Insert(device, container, force: true);
    }

    private void OnDeviceShutdown(Entity<ImperialModDeviceComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<ImperialModModuleComponent>(ent.Comp.Module, out var module) && module.DeviceEntity == ent.Owner)
            module.DeviceEntity = null;
    }

    #endregion

    #region Энергия

    public bool HasPower(EntityUid suit, float units)
    {
        if (units <= 0)
            return true;

        return _cell.HasCharge(suit, units * ChargeUnit);
    }

    public bool DrainPower(EntityUid suit, float units)
    {
        if (units <= 0)
            return true;

        return _cell.TryUseCharge(suit, units * ChargeUnit);
    }

    /// <summary>Зарядить батарею костюма (модуль крутящего момента и т. п.).</summary>
    public void AddPower(EntityUid suit, float units)
    {
        if (_cell.TryGetBatteryFromSlot(suit, out var battery))
            _battery.ChangeCharge(battery.Value.Owner, units * ChargeUnit);
    }

    #endregion

    #region Эффекты и тик

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ImperialModSuitComponent>();
        while (query.MoveNext(out var uid, out var suit))
        {
            suit.Accumulator += frameTime;
            if (suit.Accumulator < 1f)
                continue;

            var elapsed = suit.Accumulator;
            suit.Accumulator = 0f;
            Process((uid, suit), elapsed);
        }
    }

    /// <summary>process(): энергия модулей и их on_active_process.</summary>
    private void Process(Entity<ImperialModSuitComponent> suit, float seconds)
    {
        if (GetWearer(suit) is not { } wearer)
            return;

        var active = IsActive(suit);
        foreach (var uid in suit.Comp.Modules.ContainedEntities.ToArray())
        {
            if (!TryComp<ImperialModModuleComponent>(uid, out var module))
                continue;

            var ent = new Entity<ImperialModModuleComponent>(uid, module);
            var works = (active || module.AllowInactive) && HasParts(suit, module.RequiredParts);
            if (!works)
            {
                Deactivate(suit, ent, wearer, true);
                RemoveEffects(suit, ent);
                continue;
            }

            if (module.Active)
            {
                if (!DrainPower(suit, module.ActivePower * seconds))
                {
                    Deactivate(suit, ent, wearer, true);
                    continue;
                }
            }
            else if (!DrainPower(suit, module.IdlePower * seconds) && module.ModuleType == ImperialModModuleType.Passive)
            {
                RemoveEffects(suit, ent);
                continue;
            }

            UpdateEffects(suit, ent, wearer);

            if (module.EffectApplied)
            {
                var ev = new ImperialModModuleProcessEvent(suit, wearer, seconds);
                RaiseLocalEvent(uid, ref ev);
            }
        }
    }

    /// <summary>Пересчитать эффекты всех модулей сразу (после переключения).</summary>
    public void Refresh(Entity<ImperialModSuitComponent> suit)
    {
        var wearer = GetWearer(suit);
        var active = IsActive(suit);
        foreach (var uid in suit.Comp.Modules.ContainedEntities)
        {
            if (!TryComp<ImperialModModuleComponent>(uid, out var module))
                continue;

            var ent = new Entity<ImperialModModuleComponent>(uid, module);
            if (wearer == null || !(active || module.AllowInactive) || !HasParts(suit, module.RequiredParts))
            {
                RemoveEffects(suit, ent);
                continue;
            }

            UpdateEffects(suit, ent, wearer.Value);
        }
    }

    private void UpdateEffects(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid wearer)
    {
        var on = module.Comp.ModuleType == ImperialModModuleType.Passive
                 || module.Comp.ModuleType != ImperialModModuleType.Usable && module.Comp.Active;

        if (on)
            ApplyEffects(suit, module, wearer);
        else
            RemoveEffects(suit, module);
    }

    private void ApplyEffects(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module, EntityUid wearer)
    {
        if (module.Comp.EffectApplied)
            return;

        module.Comp.EffectApplied = true;
        if (GetPart(suit, module.Comp.EffectPart) is { } part)
            AddRegistry(part, module.Comp.PartComponents, module.Comp);
        AddRegistry(wearer, module.Comp.WearerComponents, module.Comp);

        var ev = new ImperialModModuleToggledEvent(suit, wearer, true);
        RaiseLocalEvent(module, ref ev);
    }

    private void RemoveEffects(Entity<ImperialModSuitComponent> suit, Entity<ImperialModModuleComponent> module)
    {
        if (!module.Comp.EffectApplied)
            return;

        module.Comp.EffectApplied = false;
        foreach (var (target, type) in module.Comp.AppliedComponents)
        {
            if (!TerminatingOrDeleted(target))
                RemComp(target, type);
        }

        module.Comp.AppliedComponents.Clear();

        var ev = new ImperialModModuleToggledEvent(suit, suit.Comp.Wearer, false);
        RaiseLocalEvent(module, ref ev);
    }

    private void AddRegistry(EntityUid target, ComponentRegistry registry, ImperialModModuleComponent module)
    {
        foreach (var (name, entry) in registry)
        {
            var type = _factory.GetRegistration(name).Type;
            if (HasComp(target, type))
                continue;

            EntityManager.AddComponent(target, entry);
            module.AppliedComponents.Add((target, type));
        }
    }

    #endregion
}
