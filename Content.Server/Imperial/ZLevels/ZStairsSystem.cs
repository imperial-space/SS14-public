using System.Numerics;
using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Examine;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Лестницы между этажами. Направление подъёма — куда повёрнута лестница (относительно своей сетки, так что на
/// повёрнутой станции она смотрит вдоль станции). Ушёл с последней ступени цепочки вперёд — оказался этажом выше
/// на клетке перед лестницей. Наверху над последней ступенью — площадка: зашёл на неё с этажа, идя назад, — спустился
/// на лестницу. Проём в потолке не нужен, но если он есть, упавший в него тоже мягко приземляется на лестницу,
/// а без сознания кубарем катится вниз (как в SS13).
/// </summary>
public sealed class ZStairsSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    private const string TopPrototype = "ZStairsTopMarker";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZStairsComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ZStairsComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ZStairsComponent, AnchorStateChangedEvent>(OnAnchor);
        SubscribeLocalEvent<ZStairsComponent, MoveEvent>(OnMoved);
        SubscribeLocalEvent<ZStairsComponent, StepTriggerAttemptEvent>(OnStepAttempt);
        SubscribeLocalEvent<ZStairsComponent, StepTriggeredOffEvent>(OnStepOff);
        SubscribeLocalEvent<ZStairsComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ZStairsTopComponent, StepTriggerAttemptEvent>(OnTopStepAttempt);
        SubscribeLocalEvent<ZStairsTopComponent, StepTriggeredOnEvent>(OnTopStep);
        SubscribeLocalEvent<ZLevelsRelinkedEvent>(OnRelinked);
    }

    private void OnMapInit(Entity<ZStairsComponent> ent, ref MapInitEvent args)
    {
        UpdateSurrounding(ent);
        UpdateTop(ent);
    }

    private void OnShutdown(Entity<ZStairsComponent> ent, ref ComponentShutdown args) => RemoveTop(ent);

    private void OnAnchor(Entity<ZStairsComponent> ent, ref AnchorStateChangedEvent args)
    {
        UpdateSurrounding(ent);
        UpdateTop(ent);
    }

    private void OnMoved(Entity<ZStairsComponent> ent, ref MoveEvent args)
    {
        if (args.NewRotation == args.OldRotation)
            return;
        UpdateSurrounding(ent);
        UpdateTop(ent);
    }

    private void OnRelinked(ZLevelsRelinkedEvent args)
    {
        var query = EntityQueryEnumerator<ZStairsComponent>();
        while (query.MoveNext(out var uid, out var stairs))
            UpdateTop((uid, stairs));
    }

    // ---------- Направление ----------

    /// <summary>Направление подъёма в клетках своей сетки.</summary>
    public Direction GetDir(EntityUid uid)
    {
        return Transform(uid).LocalRotation.GetCardinalDir();
    }

    /// <summary>Направление подъёма в мире (с поворотом сетки).</summary>
    public Vector2 GetWorldDir(EntityUid uid)
    {
        var xform = Transform(uid);
        var parentRot = xform.ParentUid.IsValid() ? _xform.GetWorldRotation(xform.ParentUid) : Angle.Zero;
        return parentRot.RotateVec(GetDir(uid).ToVec());
    }

    // ---------- Внешний вид: левые, средние, правые ступени ----------

    private IEnumerable<Entity<ZStairsComponent>> StairsAt(EntityUid near, Vector2 offset)
    {
        var xform = Transform(near);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            yield break;
        var idx = _map.TileIndicesFor(gridUid, grid, xform.Coordinates) + new Vector2i((int) MathF.Round(offset.X), (int) MathF.Round(offset.Y));
        foreach (var other in _map.GetAnchoredEntities(gridUid, grid, idx))
        {
            if (TryComp<ZStairsComponent>(other, out var stairs))
                yield return (other, stairs);
        }
    }

    private bool HasStairsWithDir(EntityUid near, Vector2 offset, Direction dir)
    {
        foreach (var s in StairsAt(near, offset))
        {
            if (GetDir(s) == dir)
                return true;
        }
        return false;
    }

    private void UpdateSurrounding(Entity<ZStairsComponent> ent)
    {
        UpdateIcon(ent);
        var dir = GetDir(ent);
        var (left, right) = Sides(dir);
        foreach (var s in StairsAt(ent, left))
            UpdateIcon(s);
        foreach (var s in StairsAt(ent, right))
            UpdateIcon(s);
        // Цепочка изменилась — у соседей спереди и сзади могла смениться последняя ступень.
        foreach (var s in StairsAt(ent, dir.ToVec()))
            UpdateTop(s);
        foreach (var s in StairsAt(ent, -dir.ToVec()))
            UpdateTop(s);
    }

    /// <summary>turn(dir, 90) и turn(dir, -90): клетка слева и справа от направления подъёма.</summary>
    private static (Vector2 Left, Vector2 Right) Sides(Direction dir)
    {
        var v = dir.ToVec();
        return (new Vector2(-v.Y, v.X), new Vector2(v.Y, -v.X));
    }

    private void UpdateIcon(Entity<ZStairsComponent> ent)
    {
        var state = ent.Comp.BaseState;
        if (ent.Comp.MergedSprites)
        {
            var dir = GetDir(ent);
            var (left, right) = Sides(dir);
            var hasLeft = HasStairsWithDir(ent, left, dir);
            var hasRight = HasStairsWithDir(ent, right, dir);
            if (hasLeft && hasRight)
                state += "-m";
            else if (hasLeft)
                state += "-r";
            else if (hasRight)
                state += "-l";
        }
        _appearance.SetData(ent, ZStairsVisuals.State, state);
    }

    // ---------- Подъём ----------

    /// <summary>isTerminator: впереди нет ступени того же направления — эта ступень последняя.</summary>
    public bool IsTerminator(Entity<ZStairsComponent> ent)
    {
        var dir = GetDir(ent);
        return !HasStairsWithDir(ent, dir.ToVec(), dir);
    }

    private void OnStepAttempt(Entity<ZStairsComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = HasComp<MobStateComponent>(args.Tripper) || HasComp<PullableComponent>(args.Tripper);
    }

    /// <summary>on_exit_stairs: ушёл с последней ступени в сторону подъёма — поднимается.</summary>
    private void OnStepOff(Entity<ZStairsComponent> ent, ref StepTriggeredOffEvent args)
    {
        if (!IsTerminator(ent) || HasComp<ThrownItemComponent>(args.Tripper))
            return;
        var dir = GetWorldDir(ent);
        var rel = _xform.GetWorldPosition(args.Tripper) - _xform.GetWorldPosition(ent);
        var forward = Vector2.Dot(rel, dir);
        var side = MathF.Abs(rel.X * dir.Y - rel.Y * dir.X);
        if (forward < 0.4f || side > 0.6f)
            return;
        StairAscend(ent, args.Tripper);
    }

    /// <summary>
    /// stair_ascend: выходит этажом выше на клетку перед лестницей. Над ступенью и на той клетке не должно быть
    /// стены, а сама клетка должна быть полом — не проёмом, из которого сразу упадёшь обратно.
    /// </summary>
    public bool StairAscend(Entity<ZStairsComponent> ent, EntityUid climber)
    {
        if (Transform(climber).Anchored)
            return false;
        if (GetBlockReason(ent, out var target) is { } reason)
        {
            if (HasComp<MobStateComponent>(climber))
                _popup.PopupEntity(Loc.GetString(reason), climber, climber, PopupType.SmallCaution);
            return false;
        }
        var here = _z.SnapToTile(_xform.GetMapCoordinates(ent));

        EntityUid? pulled = null;
        if (TryComp<PullerComponent>(climber, out var puller))
            pulled = puller.Pulling;

        _z.ZMove(climber, target, false);
        // Тащимое выходит на ступень снизу; таскание рвётся, если они оказались на разных этажах.
        if (pulled != null)
            _xform.SetMapCoordinates(pulled.Value, here);
        return true;
    }

    /// <summary>Почему с этой ступени нельзя подняться (ключ локализации) или null; куда выйдешь наверху.</summary>
    public string? GetBlockReason(Entity<ZStairsComponent> ent, out MapCoordinates target)
    {
        target = MapCoordinates.Nullspace;
        var here = _z.SnapToTile(_xform.GetMapCoordinates(ent));
        if (_z.GetStep(here, true) is not { } above)
            return "zlevel-stairs-no-level";
        if (!_z.PassOut(here, true) || _z.HasWall(above))
            return "zlevel-stairs-ceiling";
        target = _z.SnapToTile(new MapCoordinates(above.Position + GetWorldDir(ent), above.MapId));
        if (_z.HasWall(target))
            return "zlevel-stairs-wall";
        if (_z.IsOpen(target) && _z.PassOut(target, false))
            return "zlevel-stairs-no-floor";
        return null;
    }

    private void OnExamine(Entity<ZStairsComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("zlevel-stairs-examine"));
        if (!IsTerminator(ent))
            return;
        var reason = GetBlockReason(ent, out _);
        args.PushMarkup(reason == null
            ? Loc.GetString("zlevel-stairs-ok")
            : Loc.GetString("zlevel-stairs-blocked", ("reason", Loc.GetString(reason))));
    }

    // ---------- Спуск с площадки ----------

    /// <summary>Площадка над последней ступенью — на этаже выше; пересоздаётся при смене цепочки и этажей.</summary>
    private void UpdateTop(Entity<ZStairsComponent> ent)
    {
        RemoveTop(ent);
        if (TerminatingOrDeleted(ent) || !Transform(ent).Anchored || !IsTerminator(ent))
            return;
        var here = _z.SnapToTile(_xform.GetMapCoordinates(ent));
        if (_z.GetStep(here, true) is not { } above)
            return;
        var top = Spawn(TopPrototype, above);
        EnsureComp<ZStairsTopComponent>(top).Stairs = ent;
        ent.Comp.Top = top;
    }

    private void RemoveTop(Entity<ZStairsComponent> ent)
    {
        if (ent.Comp.Top is { } top && !TerminatingOrDeleted(top))
            QueueDel(top);
        ent.Comp.Top = null;
    }

    private void OnTopStepAttempt(Entity<ZStairsTopComponent> ent, ref StepTriggerAttemptEvent args)
    {
        args.Continue = HasComp<MobStateComponent>(args.Tripper) && !HasComp<ThrownItemComponent>(args.Tripper);
    }

    /// <summary>Зашёл на площадку со стороны этажа (спереди лестницы) — спускается на последнюю ступень.</summary>
    private void OnTopStep(Entity<ZStairsTopComponent> ent, ref StepTriggeredOnEvent args)
    {
        if (ent.Comp.Stairs is not { } stairsUid || !TryComp<ZStairsComponent>(stairsUid, out var stairs)
            || !IsTerminator((stairsUid, stairs)) || Transform(args.Tripper).Anchored)
            return;
        var dir = GetWorldDir(stairsUid);
        var rel = _xform.GetWorldPosition(args.Tripper) - _xform.GetWorldPosition(ent);
        if (Vector2.Dot(rel, dir) < 0.15f)
            return;
        var target = _xform.GetMapCoordinates(stairsUid);
        _z.ZMove(args.Tripper, target, true);
    }

    // ---------- Падение на лестницу ----------

    /// <summary>
    /// intercept_zImpact: последняя ступень ловит упавшего с одного этажа (FALL_INTERCEPTED, без урона).
    /// Без сознания (или сбитый с ног) скатывается: оглушение, урон 4–8 и отлёт на 2 клетки назад.
    /// </summary>
    public bool InterceptFall(Entity<ZStairsComponent> ent, List<EntityUid> falling, int levels)
    {
        if (levels != 1 || !IsTerminator(ent))
            return false;

        foreach (var guy in falling)
        {
            if (!HasComp<MobStateComponent>(guy) || !CanFallDownStairs(guy))
                continue;
            _popup.PopupEntity(Loc.GetString("zlevel-fall-down-stairs", ("stairs", Name(ent))), guy, guy, PopupType.MediumCaution);
            OnFall(ent, guy);
        }
        return true;
    }

    private bool CanFallDownStairs(EntityUid guy)
    {
        if (TryComp<BuckleComponent>(guy, out var buckle) && buckle.Buckled)
            return false;
        if (TryComp<PullableComponent>(guy, out var pullable) && pullable.BeingPulled)
            return false;
        return _mobState.IsIncapacitated(guy) || HasComp<KnockedDownComponent>(guy);
    }

    private void OnFall(Entity<ZStairsComponent> ent, EntityUid guy)
    {
        _stun.TryAddParalyzeDuration(guy, TimeSpan.FromSeconds(2));
        _stun.TryKnockdown(guy, TimeSpan.FromSeconds(5));
        _damage.TryChangeDamage(guy, new DamageSpecifier { DamageDict = { ["Blunt"] = _random.Next(4, 9) } });
        _throwing.TryThrow(guy, -GetWorldDir(ent) * 2f, 2f, doSpin: true);
    }
}
