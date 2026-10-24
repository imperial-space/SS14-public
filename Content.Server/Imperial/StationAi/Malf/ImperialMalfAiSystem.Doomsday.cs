using Content.Server.AlertLevel;
using Content.Server.Power.Components;
using Content.Server.RoundEnd;
using Content.Shared.Access.Components;
using Content.Shared.Imperial.StationAi;
using Content.Shared.Mobs.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.StationAi.Malf;

/// <summary>Ядро-укрытие внутри взломанного ЛКП (shunt).</summary>
[RegisterComponent]
public sealed partial class ImperialMalfShuntCoreComponent : Component
{
    [ViewVariables]
    public EntityUid Apc;

    /// <summary>Покинутое ядро, куда можно вернуться.</summary>
    [ViewVariables]
    public EntityUid Home;
}

/// <summary>
/// nuke_station (устройство Судного дня, 450 секунд) и перенос в ЛКП (shunt / return_to_core).
/// </summary>
public sealed partial class ImperialMalfAiSystem
{
    [Dependency] private readonly AlertLevelSystem _alert = default!;
    [Dependency] private readonly RoundEndSystem _roundEnd = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;

    private const string DoomsdayModule = "ImperialMalfDoomsday";
    private const int DoomsdayMinimumCost = 50;
    private const int CommandApcDiscount = 20;
    private static readonly TimeSpan DoomsdayDuration = TimeSpan.FromSeconds(450);
    private static readonly TimeSpan DoomsdayAnnounceInterval = TimeSpan.FromSeconds(60);
    private static readonly EntProtoId DoomsdayAction = "ActionImperialMalfDoomsday";
    private static readonly EntProtoId ShuntAction = "ActionImperialMalfShunt";
    private static readonly EntProtoId ReturnAction = "ActionImperialMalfReturnToCore";
    private static readonly EntProtoId ShuntCoreProto = "ImperialMalfShuntCore";
    private static readonly SoundSpecifier DoomsdaySound = new SoundPathSpecifier("/Audio/Misc/delta.ogg");
    private static readonly HashSet<string> CommandAccess = new() { "Command", "Captain", "HeadOfPersonnel" };
    private const string HeldSlot = "station_ai_mind_slot";

    private void InitializeDoomsday()
    {
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfDoomsdayActionEvent>(OnDoomsday);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfShuntActionEvent>(OnShunt);
        SubscribeLocalEvent<ImperialMalfAiComponent, ImperialMalfReturnToCoreActionEvent>(OnReturnToCore);
        SubscribeLocalEvent<ImperialMalfHackedApcComponent, EntityTerminatingEvent>(OnHackedApcTerminating);
    }

    /// <summary>Цена с учётом скидки: −20 за каждый взломанный командный ЛКП, не ниже 50.</summary>
    public int GetCost(ImperialMalfAiComponent malf, ImperialMalfModulePrototype module)
    {
        if (module.ID != DoomsdayModule)
            return module.Cost;

        return Math.Max(DoomsdayMinimumCost, module.Cost - CommandApcDiscount * malf.CommandApcs.Count);
    }

    /// <summary>ЛКП в командном отсеке: рядом шлюзы или консоли с командным доступом.</summary>
    private void DoomsdayApcHacked(Entity<ImperialMalfAiComponent> ent, EntityUid apc)
    {
        if (ent.Comp.Shunt == null && ent.Comp.CanShunt)
            _actions.AddAction(ent, ref ent.Comp.ShuntAction, ShuntAction);

        foreach (var reader in _lookup.GetEntitiesInRange<AccessReaderComponent>(Transform(apc).Coordinates, 5f))
        {
            foreach (var list in reader.Comp.AccessLists)
            {
                foreach (var access in list)
                {
                    if (!CommandAccess.Contains(access.Id))
                        continue;

                    if (ent.Comp.CommandApcs.Add(apc))
                        Notify(ent, Loc.GetString("imperial-malf-command-apc", ("discount", CommandApcDiscount)));
                    return;
                }
            }
        }
    }

    private void DoomsdayBought(Entity<ImperialMalfAiComponent> ent)
    {
        _actions.AddAction(ent, ref ent.Comp.DoomsdayAction, DoomsdayAction);
    }

    /// <summary>nuke_station(): запуск отсчёта. Ядро должно оставаться на станции.</summary>
    private void OnDoomsday(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfDoomsdayActionEvent args)
    {
        if (args.Handled || ent.Comp.DoomsdayEnd != null)
            return;

        if (ent.Comp.Shunt != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-doomsday-shunted"), ent, ent);
            return;
        }

        if (!_ai.TryGetCore(ent, out var core) || _station.GetOwningStation(core.Owner) is not { } station)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-doomsday-off-station"), ent, ent);
            return;
        }

        args.Handled = true;
        _actions.RemoveAction(ent.Owner, ent.Comp.DoomsdayAction);
        ent.Comp.DoomsdayAction = null;
        ent.Comp.DoomsdayEnd = _timing.CurTime + DoomsdayDuration;
        ent.Comp.NextDoomsdayAnnounce = _timing.CurTime + DoomsdayAnnounceInterval;
        ent.Comp.CanShunt = false;
        if (ent.Comp.ShuntAction != null)
        {
            _actions.RemoveAction(ent.Owner, ent.Comp.ShuntAction);
            ent.Comp.ShuntAction = null;
        }

        _alert.SetLevel(station, "delta", false, true, true, true);
        _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-malf-doomsday-announce"),
            Loc.GetString("imperial-malf-doomsday-sender"), false, colorOverride: Color.Red);
        _audio.PlayGlobal(DoomsdaySound, Robust.Shared.Player.Filter.Broadcast(), true);
        Notify(ent, Loc.GetString("imperial-malf-doomsday-self"));
        Log.Info($"{ToPrettyString(ent)} запустил устройство Судного дня");
    }

    private void UpdateDoomsday(TimeSpan now)
    {
        var query = EntityQueryEnumerator<ImperialMalfAiComponent>();
        var done = new List<Entity<ImperialMalfAiComponent>>();
        while (query.MoveNext(out var uid, out var malf))
        {
            if (malf.DoomsdayEnd is not { } end)
                continue;

            // Ядро уничтожено, ИИ мёртв или увезён — устройство отключается.
            if (!_ai.TryGetCore(uid, out var core) || _mobs.IsDead(uid) || _station.GetOwningStation(core.Owner) == null)
            {
                malf.DoomsdayEnd = null;
                _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-malf-doomsday-stopped"),
                    Loc.GetString("imperial-malf-doomsday-sender"), colorOverride: Color.Green);
                continue;
            }

            if (now >= end)
            {
                done.Add((uid, malf));
                continue;
            }

            if (now < malf.NextDoomsdayAnnounce)
                continue;

            malf.NextDoomsdayAnnounce = now + DoomsdayAnnounceInterval;
            var left = (int) Math.Ceiling((end - now).TotalSeconds);
            _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-malf-doomsday-countdown", ("seconds", left)),
                Loc.GetString("imperial-malf-doomsday-sender"), colorOverride: Color.Red);
        }

        foreach (var ai in done)
        {
            Detonate(ai);
        }
    }

    /// <summary>detonate(): вся органическая жизнь станции обращается в прах, раунд завершается.</summary>
    private void Detonate(Entity<ImperialMalfAiComponent> ent)
    {
        ent.Comp.DoomsdayEnd = null;
        if (!_ai.TryGetCore(ent, out var core) || _station.GetOwningStation(core.Owner) is not { } station)
            return;

        var victims = new List<EntityUid>();
        var query = EntityQueryEnumerator<MobStateComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (HasComp<BorgChassisComponent>(uid) || HasComp<StationAiHeldComponent>(uid) || _station.GetOwningStation(uid) != station)
                continue;

            victims.Add(uid);
        }

        foreach (var victim in victims)
        {
            Spawn("Ash", _transform.GetMapCoordinates(victim));
            QueueDel(victim);
        }

        Log.Info($"Устройство Судного дня {ToPrettyString(ent)} сработало: {victims.Count} жертв");
        _chat.DispatchGlobalAnnouncement(Loc.GetString("imperial-malf-doomsday-detonated"),
            Loc.GetString("imperial-malf-doomsday-sender"), colorOverride: Color.Red);
        _roundEnd.EndRound(TimeSpan.FromSeconds(15));
    }

    #region Перенос в ЛКП

    /// <summary>shunt: разум переносится в ядро-укрытие внутри взломанного ЛКП.</summary>
    private void OnShunt(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfShuntActionEvent args)
    {
        if (PickTarget(args.Target, HasComp<ApcComponent>) is not { } target)
        {
            if (!args.Handled)
                _popup.PopupEntity(Loc.GetString("imperial-malf-no-target"), ent, ent);
            return;
        }

        if (args.Handled)
            return;

        if (!ent.Comp.CanShunt || ent.Comp.Shunt != null)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-shunt-blocked"), ent, ent);
            return;
        }

        if (!TryComp<ImperialMalfHackedApcComponent>(target, out var hacked) || hacked.Ai != ent.Owner)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-shunt-not-hacked"), ent, ent);
            return;
        }

        if (!_ai.TryGetCore(ent, out var home))
            return;

        args.Handled = true;
        var shunt = Spawn(ShuntCoreProto, Transform(target).Coordinates);
        if (!MoveBrain(ent, home.Owner, shunt))
        {
            QueueDel(shunt);
            return;
        }

        _power.SetNeedsPower(shunt, false);
        var comp = EnsureComp<ImperialMalfShuntCoreComponent>(shunt);
        comp.Apc = target;
        comp.Home = home.Owner;
        ent.Comp.Shunt = shunt;
        _actions.AddAction(ent, ref ent.Comp.ReturnAction, ReturnAction);
        Notify(ent, Loc.GetString("imperial-malf-shunt-done", ("apc", Name(target))));
    }

    /// <summary>return_to_core: назад в покинутое ядро, если оно цело.</summary>
    private void OnReturnToCore(Entity<ImperialMalfAiComponent> ent, ref ImperialMalfReturnToCoreActionEvent args)
    {
        if (args.Handled || ent.Comp.Shunt is not { } shunt || !TryComp<ImperialMalfShuntCoreComponent>(shunt, out var comp))
            return;

        if (TerminatingOrDeleted(comp.Home) || !_containers.TryGetContainer(comp.Home, HeldSlot, out var slot) || slot.ContainedEntities.Count > 0)
        {
            _popup.PopupEntity(Loc.GetString("imperial-malf-return-no-core"), ent, ent);
            return;
        }

        args.Handled = true;
        if (!MoveBrain(ent, shunt, comp.Home))
            return;

        ent.Comp.Shunt = null;
        _actions.RemoveAction(ent.Owner, ent.Comp.ReturnAction);
        ent.Comp.ReturnAction = null;
        QueueDel(shunt);
        Notify(ent, Loc.GetString("imperial-malf-return-done"));
    }

    private bool MoveBrain(EntityUid brain, EntityUid from, EntityUid to)
    {
        if (!_containers.TryGetContainer(from, HeldSlot, out var source) || !_containers.TryGetContainer(to, HeldSlot, out var target))
            return false;

        if (!_containers.Remove(brain, source))
            return false;

        if (_containers.Insert(brain, target))
            return true;

        _containers.Insert(brain, source);
        return false;
    }

    /// <summary>ЛКП с укрытием уничтожен — ИИ погибает вместе с ним.</summary>
    private void OnHackedApcTerminating(Entity<ImperialMalfHackedApcComponent> ent, ref EntityTerminatingEvent args)
    {
        var query = EntityQueryEnumerator<ImperialMalfShuntCoreComponent>();
        while (query.MoveNext(out var uid, out var shunt))
        {
            if (shunt.Apc != ent.Owner)
                continue;

            if (TryComp<ImperialMalfAiComponent>(ent.Comp.Ai, out var malf))
                malf.Shunt = null;

            Log.Info($"ЛКП {ToPrettyString(ent)} с укрытием сбойного ИИ уничтожен");
            QueueDel(uid);
        }
    }

    #endregion
}
