using Content.Server.Atmos.EntitySystems;
using Content.Server.Imperial.Lavaland.LavalandShuttle;
using Content.Server.Station.Systems;
using Content.Shared.Atmos;
using Content.Shared.Implants;
using Content.Shared.Implants.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.UserInterface;
using Content.Shared.Warps;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Security;

/// <summary>Имплант изгнания (implant/exile).</summary>
[RegisterComponent]
public sealed partial class ExileImplantComponent : Component;

/// <summary>Имплант запрета телепортации (implant/teleport_blocker).</summary>
[RegisterComponent]
public sealed partial class TeleportBlockerImplantComponent : Component;

/// <summary>Имплант-маяк (implant/beacon): носитель — точка для телепортации.</summary>
[RegisterComponent]
public sealed partial class BeaconImplantComponent : Component
{
    /// <summary>lifespan_postmortem: 10 минут после смерти.</summary>
    [DataField]
    public TimeSpan LifespanPostmortem = TimeSpan.FromMinutes(10);

    [ViewVariables]
    public TimeSpan? DiedAt;

    /// <summary>Точку телепортации добавил сам имплант (у моба её раньше не было).</summary>
    [ViewVariables]
    public bool AddedWarp;
}

/// <summary>Метка на носителе: следит за мгновенными перемещениями.</summary>
[RegisterComponent]
public sealed partial class TeleportGuardComponent : Component
{
    [ViewVariables]
    public int Exile;

    [ViewVariables]
    public int Blockers;

    /// <summary>Куда вернуть после пресечённого телепорта.</summary>
    [ViewVariables]
    public MapCoordinates? RevertTo;

    [ViewVariables]
    public bool RevertStun;

    [ViewVariables]
    public bool Reverting;
}

/// <summary>
/// Импланты СБ: изгнание (нельзя вернуться на станцию с другой карты и пользоваться шаттлом Лаваленда),
/// запрет телепортации (любой телепорт отбрасывает назад с параличом на 5 с) и маяк.
/// В SS14 нет общего события телепортации, поэтому телепорт определяется по скачку позиции.
/// </summary>
public sealed class SecurityImplantSystem : EntitySystem
{
    /// <summary>Скачок больше этого расстояния за одно перемещение считается телепортом.</summary>
    private const float TeleportDistance = 3f;

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    /// <summary>Сущности, которым разрешено одно «законное» перемещение (отправка в лагерь и возврат).</summary>
    private readonly HashSet<EntityUid> _allowed = new();

    private TimeSpan _nextBeaconCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ExileImplantComponent, ImplantImplantedEvent>(OnExileImplanted);
        SubscribeLocalEvent<ExileImplantComponent, ImplantRemovedEvent>(OnExileRemoved);
        SubscribeLocalEvent<TeleportBlockerImplantComponent, ImplantImplantedEvent>(OnBlockerImplanted);
        SubscribeLocalEvent<TeleportBlockerImplantComponent, ImplantRemovedEvent>(OnBlockerRemoved);
        SubscribeLocalEvent<BeaconImplantComponent, ImplantImplantedEvent>(OnBeaconImplanted);
        SubscribeLocalEvent<BeaconImplantComponent, ImplantRemovedEvent>(OnBeaconRemoved);

        SubscribeLocalEvent<TeleportGuardComponent, MoveEvent>(OnMove);
        SubscribeLocalEvent<TeleportGuardComponent, UserOpenActivatableUIAttemptEvent>(OnOpenUiAttempt);
    }

    /// <summary>Разрешить сущности одно перемещение без проверки (телепорт гулага).</summary>
    public void AllowNextMove(EntityUid uid)
    {
        _allowed.Add(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var guards = EntityQueryEnumerator<TeleportGuardComponent>();
        while (guards.MoveNext(out var uid, out var guard))
        {
            if (guard.RevertTo is not { } coords)
                continue;

            guard.RevertTo = null;
            if (!_mapCheck(coords.MapId))
                continue;

            guard.Reverting = true;
            _xform.SetMapCoordinates(uid, coords);
            guard.Reverting = false;

            Spawn("EffectSparks", Transform(uid).Coordinates);
            if (guard.RevertStun)
            {
                _stun.TryUpdateParalyzeDuration(uid, TimeSpan.FromSeconds(5));
                _popup.PopupEntity(Loc.GetString("security-implant-teleport-blocked"), uid, uid, PopupType.LargeCaution);
            }
            else
            {
                _popup.PopupEntity(Loc.GetString("security-implant-exile-blocked"), uid, uid, PopupType.MediumCaution);
            }
        }

        _allowed.Clear();

        if (_timing.CurTime < _nextBeaconCheck)
            return;

        _nextBeaconCheck = _timing.CurTime + TimeSpan.FromSeconds(1);
        var beacons = EntityQueryEnumerator<BeaconImplantComponent, SubdermalImplantComponent>();
        while (beacons.MoveNext(out _, out var beacon, out var implant))
        {
            if (implant.ImplantedEntity is not { } mob)
                continue;

            if (!_mobState.IsDead(mob))
            {
                beacon.DiedAt = null;
                continue;
            }

            beacon.DiedAt ??= _timing.CurTime;
            if (beacon.AddedWarp && _timing.CurTime > beacon.DiedAt + beacon.LifespanPostmortem)
            {
                RemComp<WarpPointComponent>(mob);
                beacon.AddedWarp = false;
            }
        }
    }

    private bool _mapCheck(MapId map)
    {
        return map != MapId.Nullspace && EntityManager.System<SharedMapSystem>().MapExists(map);
    }

    #region Вживление

    private void OnExileImplanted(Entity<ExileImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        EnsureComp<TeleportGuardComponent>(args.Implanted).Exile++;
    }

    private void OnExileRemoved(Entity<ExileImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (TryComp<TeleportGuardComponent>(args.Implanted, out var guard))
        {
            guard.Exile--;
            CleanupGuard((args.Implanted, guard));
        }
    }

    private void OnBlockerImplanted(Entity<TeleportBlockerImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        EnsureComp<TeleportGuardComponent>(args.Implanted).Blockers++;
    }

    private void OnBlockerRemoved(Entity<TeleportBlockerImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (TryComp<TeleportGuardComponent>(args.Implanted, out var guard))
        {
            guard.Blockers--;
            CleanupGuard((args.Implanted, guard));
        }
    }

    private void CleanupGuard(Entity<TeleportGuardComponent> ent)
    {
        if (ent.Comp.Exile <= 0 && ent.Comp.Blockers <= 0)
            RemCompDeferred<TeleportGuardComponent>(ent);
    }

    /// <summary>implant/beacon: носитель появляется в списках точек телепортации.</summary>
    private void OnBeaconImplanted(Entity<BeaconImplantComponent> ent, ref ImplantImplantedEvent args)
    {
        if (HasComp<WarpPointComponent>(args.Implanted))
            return;

        var warp = AddComp<WarpPointComponent>(args.Implanted);
        warp.Location = Loc.GetString("security-implant-beacon-warp", ("name", Name(args.Implanted)));
        warp.Follow = true;
        ent.Comp.AddedWarp = true;
    }

    private void OnBeaconRemoved(Entity<BeaconImplantComponent> ent, ref ImplantRemovedEvent args)
    {
        if (!ent.Comp.AddedWarp)
            return;

        RemComp<WarpPointComponent>(args.Implanted);
        ent.Comp.AddedWarp = false;
    }

    #endregion

    #region Перемещения

    /// <summary>
    /// COMSIG_MOVABLE_TELEPORTING: мгновенный скачок позиции — телепорт.
    /// Запрет телепортации отбрасывает назад всегда, изгнание — только при попадании на станцию с другой карты.
    /// </summary>
    private void OnMove(Entity<TeleportGuardComponent> ent, ref MoveEvent args)
    {
        if (ent.Comp.Reverting || ent.Comp.RevertTo != null || _allowed.Contains(ent))
            return;

        if (!args.OldPosition.IsValid(EntityManager) || !args.NewPosition.IsValid(EntityManager))
            return;

        var oldMap = _xform.ToMapCoordinates(args.OldPosition);
        var newMap = _xform.ToMapCoordinates(args.NewPosition);
        var teleported = oldMap.MapId != newMap.MapId || (oldMap.Position - newMap.Position).Length() > TeleportDistance;
        if (!teleported)
            return;

        if (ent.Comp.Blockers > 0)
        {
            ent.Comp.RevertTo = oldMap;
            ent.Comp.RevertStun = true;
            return;
        }

        if (ent.Comp.Exile > 0 && oldMap.MapId != newMap.MapId && IsStation(args.NewPosition))
        {
            ent.Comp.RevertTo = oldMap;
            ent.Comp.RevertStun = false;
        }
    }

    private bool IsStation(EntityCoordinates coords)
    {
        var grid = _xform.GetGrid(coords);
        return grid != null && _station.GetOwningStation(grid.Value) != null;
    }

    /// <summary>Изгнанник не может управлять шаттлом Лаваленда.</summary>
    private void OnOpenUiAttempt(Entity<TeleportGuardComponent> ent, ref UserOpenActivatableUIAttemptEvent args)
    {
        if (ent.Comp.Exile <= 0 || !HasComp<LavalandShuttleConsoleComponent>(args.Target))
            return;

        args.Cancel();
        _popup.PopupEntity(Loc.GetString("security-implant-exile-shuttle"), ent, ent, PopupType.SmallCaution);
    }

    #endregion

    #region Консоль заключённых

    /// <summary>beacon/is_shown_on_console(): виден, пока носитель жив и ещё 10 минут после смерти.</summary>
    public bool IsBeaconShown(Entity<BeaconImplantComponent> ent)
    {
        return ent.Comp.DiedAt is not { } died || _timing.CurTime <= died + ent.Comp.LifespanPostmortem;
    }

    /// <summary>beacon/get_management_console_data(): безопасна ли обстановка вокруг носителя.</summary>
    public string GetBeaconStatus(EntityUid mob)
    {
        if (TryComp<TeleportGuardComponent>(mob, out var guard) && guard.Blockers > 0)
            return Loc.GetString("security-implant-beacon-unreachable");

        var mixture = _atmos.GetContainingMixture(mob, excite: false);
        if (mixture == null)
            return Loc.GetString("security-implant-beacon-hazard");

        var pressure = mixture.Pressure;
        var oxygen = mixture.GetMoles(Gas.Oxygen) / Math.Max(mixture.TotalMoles, 0.001f) * pressure;
        var plasma = mixture.GetMoles(Gas.Plasma) / Math.Max(mixture.TotalMoles, 0.001f) * pressure;
        var safe = pressure is > 50f and < 300f && mixture.Temperature is > 260f and < 360f && oxygen > 16f && plasma < 0.5f;
        return Loc.GetString(safe ? "security-implant-beacon-safe" : "security-implant-beacon-hazard");
    }

    #endregion
}
