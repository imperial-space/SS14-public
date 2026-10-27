using Content.Server.NPC.HTN;
using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.DoAfter;
using Content.Shared.Ghost;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.Silicons.StationAi;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Действия «подняться/опуститься на этаж», выданные призраку, ИИ или летящему на джетпаке.</summary>
[RegisterComponent]
public sealed partial class ZFlyActionsComponent : Component
{
    [ViewVariables]
    public EntityUid? Up;

    [ViewVariables]
    public EntityUid? Down;
}

/// <summary>
/// Перемещение между этажами без лестниц (SS13 Move Up / Move Down):
/// призраки и админы — свободно; камера ИИ — свободно (обзор всё равно через камеры этажа);
/// на включённом джетпаке — только сквозь проём (вверх — если над тобой открыто, вниз — если открыто под тобой).
/// Крюк-кошка (climbing hook с Icebox) — вылезти через проём над собой (или рядом) на пол этажом выше.
/// Мобы под управлением ИИ обходят открытое пространство (<see cref="ZAvoidOpenSpaceComponent"/>).
/// </summary>
public sealed class ZMovementSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedStationAiSystem _stationAi = default!;

    private const string MoveUpAction = "ActionZMoveUp";
    private const string MoveDownAction = "ActionZMoveDown";
    private TimeSpan _next;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZMoveUpActionEvent>(e => OnMoveAction(e, true));
        SubscribeLocalEvent<ZMoveDownActionEvent>(e => OnMoveAction(e, false));
        SubscribeLocalEvent<ZClimbingHookComponent, UseInHandEvent>(OnHookUse);
        SubscribeLocalEvent<ZClimbingHookComponent, ZClimbHookDoAfterEvent>(OnHookDone);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + TimeSpan.FromSeconds(1);
        UpdateFlyActions();
        UpdateNpcAvoidance();
    }

    // ---------- Подняться / опуститься ----------

    private bool OnLevels(EntityUid uid)
    {
        return Transform(uid).MapUid is { } map && TryComp<ZLevelMapComponent>(map, out var z) && (z.Above != null || z.Below != null);
    }

    /// <summary>Кто перемещается по этажам за игрока: камера ИИ, а не мозг в ядре.</summary>
    private EntityUid? GetMover(EntityUid uid)
    {
        if (HasComp<StationAiHeldComponent>(uid))
            return _stationAi.TryGetCore(uid, out var core) && core.Comp?.RemoteEntity is { } eye ? eye : null;
        return uid;
    }

    private void UpdateFlyActions()
    {
        var query = EntityQueryEnumerator<ActorComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            var mover = GetMover(uid);
            var want = mover != null && OnLevels(mover.Value)
                       && (HasComp<GhostComponent>(uid) || HasComp<StationAiHeldComponent>(uid) || HasComp<JetpackUserComponent>(uid));
            if (want)
            {
                var comp = EnsureComp<ZFlyActionsComponent>(uid);
                if (comp.Up == null)
                    _actions.AddAction(uid, ref comp.Up, MoveUpAction);
                if (comp.Down == null)
                    _actions.AddAction(uid, ref comp.Down, MoveDownAction);
            }
            else if (TryComp<ZFlyActionsComponent>(uid, out var comp))
            {
                _actions.RemoveAction(uid, comp.Up);
                _actions.RemoveAction(uid, comp.Down);
                RemComp<ZFlyActionsComponent>(uid);
            }
        }
    }

    private void OnMoveAction(InstantActionEvent args, bool up)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.Performer;
        if (GetMover(user) is not { } mover)
            return;

        var here = _xform.GetMapCoordinates(mover);
        if (_z.GetStep(here, up) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString(up ? "zlevel-nothing-above" : "zlevel-nothing-below"), user, user);
            return;
        }

        // Призраку и камере ИИ перекрытия не мешают; на джетпаке летят только сквозь проём.
        var free = HasComp<GhostComponent>(user) || HasComp<StationAiHeldComponent>(user);
        if (!free)
        {
            var tile = _z.SnapToTile(here);
            var targetTile = _z.SnapToTile(target);
            if (!_z.PassOut(tile, up) || !_z.PassIn(targetTile, up) || up && !_z.IsOpen(targetTile))
            {
                _popup.PopupEntity(Loc.GetString(up ? "zlevel-fly-blocked-up" : "zlevel-fly-blocked-down"), user, user);
                return;
            }
        }

        _z.ZMove(mover, target, false);
        _popup.PopupEntity(Loc.GetString(up ? "zlevel-fly-up" : "zlevel-fly-down"), user, user, PopupType.Small);
    }

    // ---------- Крюк-кошка ----------

    private void OnHookUse(Entity<ZClimbingHookComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;
        args.Handled = true;
        var user = args.User;
        if (FindHookTarget(user) is not { } _)
        {
            _popup.PopupEntity(Loc.GetString("zlevel-hook-no-hole"), user, user);
            return;
        }
        if (TryComp<BuckleComponent>(user, out var buckle) && buckle.Buckled)
            return;
        _popup.PopupEntity(Loc.GetString("zlevel-hook-throw", ("user", Name(user))), user, PopupType.Medium);
        _doAfter.TryStartDoAfter(new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new ZClimbHookDoAfterEvent(), ent, used: ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnHookDone(Entity<ZClimbingHookComponent> ent, ref ZClimbHookDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;
        args.Handled = true;
        if (FindHookTarget(args.User) is not { } landing)
        {
            _popup.PopupEntity(Loc.GetString("zlevel-hook-no-hole"), args.User, args.User);
            return;
        }
        _z.ZMove(args.User, landing, false);
        _popup.PopupEntity(Loc.GetString("zlevel-hook-climbed"), args.User, args.User, PopupType.Medium);
    }

    /// <summary>Проём над собой или в соседней клетке и пол рядом с ним наверху, куда можно вылезти.</summary>
    private MapCoordinates? FindHookTarget(EntityUid user)
    {
        var here = _z.SnapToTile(_xform.GetMapCoordinates(user));
        for (var dx = 0; dx <= 2; dx++)
        for (var dy = 0; dy <= 2; dy++)
        {
            // Сначала клетка над собой, потом соседние.
            var ox = dx == 2 ? -1 : dx;
            var oy = dy == 2 ? -1 : dy;
            var cell = new MapCoordinates(here.Position + new System.Numerics.Vector2(ox, oy), here.MapId);
            if (_z.GetStep(cell, true) is not { } hole || !_z.IsOpen(hole) || _z.HasWall(hole))
                continue;
            for (var lx = -1; lx <= 1; lx++)
            for (var ly = -1; ly <= 1; ly++)
            {
                if (lx == 0 && ly == 0)
                    continue;
                var landing = _z.SnapToTile(new MapCoordinates(hole.Position + new System.Numerics.Vector2(lx, ly), hole.MapId));
                if (!_z.IsOpen(landing) && !_z.HasWall(landing))
                    return landing;
            }
        }
        return null;
    }

    // ---------- Мобы обходят дыры ----------

    private void UpdateNpcAvoidance()
    {
        var query = EntityQueryEnumerator<HTNComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            var avoid = xform.MapUid is { } map && TryComp<ZLevelMapComponent>(map, out var z) && z.Below != null
                        && !HasComp<ActorComponent>(uid)
                        && !(TryComp<PullableComponent>(uid, out var pullable) && pullable.BeingPulled)
                        && !(TryComp<BuckleComponent>(uid, out var buckle) && buckle.Buckled);
            if (avoid)
                EnsureComp<ZAvoidOpenSpaceComponent>(uid);
            else
                RemComp<ZAvoidOpenSpaceComponent>(uid);
        }
    }
}
