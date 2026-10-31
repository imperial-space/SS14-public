using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Interaction;
using Content.Shared.Examine;
using Content.Shared.Movement.Pulling.Components;
using Content.Shared.Movement.Pulling.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Грузовой лифт: платформа ездит по шахте поэтажно и везёт всё незакреплённое, что на ней стоит, — людей,
/// предметы, ящики. Шахта — открытое пространство или пустота: наверх едет, только если над всей платформой
/// проём; вниз — только если проём под ней. Стена на пути останавливает лифт.
/// Управление: клик по платформе — вверх, Alt-клик — вниз; кнопка вызова на этаже зовёт лифт к себе.
/// </summary>
public sealed class ZElevatorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly PullingSystem _pulling = default!;

    private static readonly SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/Machines/blastdoor.ogg");
    private static readonly SoundSpecifier ArriveSound = new SoundPathSpecifier("/Audio/Machines/button.ogg");

    private readonly HashSet<EntityUid> _riders = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ZElevatorComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<ZElevatorComponent, GetVerbsEvent<InteractionVerb>>(OnVerbs);
        SubscribeLocalEvent<ZElevatorComponent, GetVerbsEvent<AlternativeVerb>>(OnAltVerbs);
        SubscribeLocalEvent<ZElevatorComponent, ExaminedEvent>(OnExamine);
        SubscribeLocalEvent<ZElevatorButtonComponent, ActivateInWorldEvent>(OnButton);
    }

    // ---------- Управление ----------

    private void OnActivate(Entity<ZElevatorComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        RequestStep(ent, args.User, CanStep(ent, true) || !CanStep(ent, false));
    }

    private void OnVerbs(Entity<ZElevatorComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new InteractionVerb { Text = Loc.GetString("zlevel-elevator-up"), Act = () => RequestStep(ent, user, true) });
        args.Verbs.Add(new InteractionVerb { Text = Loc.GetString("zlevel-elevator-down"), Act = () => RequestStep(ent, user, false) });
    }

    private void OnAltVerbs(Entity<ZElevatorComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb { Text = Loc.GetString("zlevel-elevator-down"), Act = () => RequestStep(ent, user, false) });
    }

    private void OnExamine(Entity<ZElevatorComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("zlevel-elevator-examine"));
    }

    /// <summary>Кнопка вызова: ближайший лифт стопки рядом с кнопкой едет на её этаж.</summary>
    private void OnButton(Entity<ZElevatorButtonComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        _audio.PlayPvs(ArriveSound, ent);
        if (Transform(ent).MapUid is not { } map || FindElevator(ent) is not { } elevator)
        {
            _popup.PopupEntity(Loc.GetString("zlevel-elevator-none"), ent, args.User);
            return;
        }
        if (Transform(elevator).MapUid == map)
        {
            _popup.PopupEntity(Loc.GetString("zlevel-elevator-here"), ent, args.User);
            return;
        }
        Send(elevator, map);
        _popup.PopupEntity(Loc.GetString("zlevel-elevator-called"), ent, args.User);
    }

    private Entity<ZElevatorComponent>? FindElevator(Entity<ZElevatorButtonComponent> button)
    {
        var mapUid = Transform(button).MapUid;
        if (mapUid == null || !TryComp<ZLevelMapComponent>(mapUid, out var z) || string.IsNullOrEmpty(z.Stack))
            return null;
        var pos = _xform.GetWorldPosition(button);
        Entity<ZElevatorComponent>? best = null;
        var bestDist = float.MaxValue;
        var query = EntityQueryEnumerator<ZElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var xform))
        {
            if (!TryComp<ZLevelMapComponent>(xform.MapUid, out var ez) || ez.Stack != z.Stack)
                continue;
            var dist = (_xform.GetWorldPosition(xform) - pos).Length();
            if (dist <= elevator.Size / 2f * MathF.Sqrt(2) + button.Comp.Range && dist < bestDist)
            {
                best = (uid, elevator);
                bestDist = dist;
            }
        }
        return best;
    }

    private void RequestStep(Entity<ZElevatorComponent> ent, EntityUid user, bool up)
    {
        if (ent.Comp.Target != null)
        {
            _popup.PopupEntity(Loc.GetString("zlevel-elevator-busy"), ent, user);
            return;
        }
        if (Transform(ent).MapUid is not { } map || _z.GetAdjacentMap(map, up) is not { } target)
        {
            _popup.PopupEntity(Loc.GetString(up ? "zlevel-elevator-top" : "zlevel-elevator-bottom"), ent, user);
            return;
        }
        if (!CanStep(ent, up))
        {
            _popup.PopupEntity(Loc.GetString("zlevel-elevator-blocked"), ent, user);
            return;
        }
        Send(ent, target);
    }

    /// <summary>Отправить лифт на этаж (поэтажно, по TravelTime на каждый).</summary>
    public void Send(Entity<ZElevatorComponent> ent, EntityUid targetMap)
    {
        if (ent.Comp.Target == null)
        {
            ent.Comp.NextMove = _timing.CurTime + ent.Comp.TravelTime;
            _audio.PlayPvs(StartSound, ent);
            _popup.PopupEntity(Loc.GetString("zlevel-elevator-starts"), ent, PopupType.Medium);
        }
        ent.Comp.Target = targetMap;
    }

    // ---------- Движение ----------

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ZElevatorComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var elevator, out var xform))
        {
            if (elevator.Target is not { } target || now < elevator.NextMove)
                continue;
            var ent = (uid, elevator);
            if (xform.MapUid is not { } map || TerminatingOrDeleted(target)
                || !TryComp<ZLevelMapComponent>(map, out var here) || !TryComp<ZLevelMapComponent>(target, out var there))
            {
                elevator.Target = null;
                continue;
            }

            var up = there.Level > here.Level;
            if (!CanStep(ent, up))
            {
                elevator.Target = null;
                _popup.PopupEntity(Loc.GetString("zlevel-elevator-blocked"), uid, PopupType.MediumCaution);
                continue;
            }

            Step(ent, up);
            if (Transform(uid).MapUid == target)
            {
                elevator.Target = null;
                _audio.PlayPvs(ArriveSound, uid);
                _popup.PopupEntity(Loc.GetString("zlevel-elevator-arrived"), uid, PopupType.Medium);
            }
            else
                elevator.NextMove = now + elevator.TravelTime;
        }
    }

    /// <summary>Клетки платформы в мировых координатах.</summary>
    private IEnumerable<Vector2> Cells(Entity<ZElevatorComponent> ent)
    {
        var matrix = _xform.GetWorldMatrix(ent);
        var half = (ent.Comp.Size - 1) / 2f;
        for (var x = 0; x < ent.Comp.Size; x++)
        for (var y = 0; y < ent.Comp.Size; y++)
            yield return Vector2.Transform(new Vector2(x - half, y - half), matrix);
    }

    /// <summary>Вверх — над каждой клеткой платформы проём; вниз — проём под платформой. Стен на пути нет.</summary>
    public bool CanStep(Entity<ZElevatorComponent> ent, bool up)
    {
        var mapId = Transform(ent).MapID;
        foreach (var cell in Cells(ent))
        {
            var here = new MapCoordinates(cell, mapId);
            if (_z.GetStep(here, up) is not { } there)
                return false;
            if (_z.HasWall(there))
                return false;
            if (!_z.IsOpen(up ? there : here))
                return false;
        }
        return true;
    }

    /// <summary>Переносит платформу и всех, кто на ней, этажом выше или ниже — каждого в ту же точку мира.</summary>
    private void Step(Entity<ZElevatorComponent> ent, bool up)
    {
        var xform = Transform(ent);
        if (xform.MapUid is not { } map || _z.GetAdjacentMap(map, up) is not { } targetMap)
            return;
        var targetId = Comp<MapComponent>(targetMap).MapId;

        _riders.Clear();
        var inv = _xform.GetInvWorldMatrix(xform);
        var half = ent.Comp.Size / 2f;
        var (center, rotation) = _xform.GetWorldPositionRotation(xform);
        var box = Box2.CenteredAround(center, new Vector2(ent.Comp.Size * 1.5f));
        foreach (var rider in _lookup.GetEntitiesIntersecting(xform.MapID, box, LookupFlags.Uncontained | LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (rider == ent.Owner || HasComp<MapGridComponent>(rider) || HasComp<GhostComponent>(rider))
                continue;
            var rx = Transform(rider);
            if (rx.Anchored || rx.ParentUid != rx.GridUid && rx.ParentUid != rx.MapUid)
                continue;
            var local = Vector2.Transform(_xform.GetWorldPosition(rx), inv);
            if (MathF.Abs(local.X) <= half && MathF.Abs(local.Y) <= half)
                _riders.Add(rider);
        }

        MoveTo(ent, center, rotation, targetId);
        foreach (var rider in _riders)
        {
            var (pos, rot) = _xform.GetWorldPositionRotation(rider);
            MoveTo(rider, pos, rot, targetId);
        }

        // Тащить через перекрытие нельзя: кто остался на другом этаже — отпускает.
        foreach (var rider in _riders)
        {
            if (TryComp<PullableComponent>(rider, out var pullable) && pullable.Puller is { } puller && Transform(puller).MapID != targetId)
                _pulling.TryStopPull(rider, pullable);
            if (TryComp<PullerComponent>(rider, out var pullerComp) && pullerComp.Pulling is { } pulled
                && Transform(pulled).MapID != targetId && TryComp<PullableComponent>(pulled, out var pulledComp))
                _pulling.TryStopPull(pulled, pulledComp);
        }
    }

    private void MoveTo(EntityUid uid, Vector2 worldPos, Angle worldRot, MapId map)
    {
        _xform.SetMapCoordinates(uid, new MapCoordinates(worldPos, map));
        _xform.AttachToGridOrMap(uid);
        _xform.SetWorldRotation(uid, worldRot);
    }
}
