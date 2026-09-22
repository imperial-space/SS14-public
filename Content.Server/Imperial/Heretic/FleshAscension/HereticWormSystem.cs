using Content.Server.Mind;
using Content.Shared.Actions;
using Robust.Shared.Prototypes;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using System.Numerics;

namespace Content.Server.Imperial.Heretic.FleshAscension;

public sealed class HereticWormSystem : EntitySystem
{
    private static readonly EntProtoId ShedHumanFormProto = "ActionHereticShedHumanForm";

    [Dependency] private readonly SharedActionsSystem   _actions   = default!;
    [Dependency] private readonly MindSystem            _mind      = default!;
    [Dependency] private readonly MobStateSystem        _mobState  = default!;
    [Dependency] private readonly SharedPopupSystem     _popup     = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private const string WormProto        = "MobHereticFleshWorm";
    private const string WormSegmentProto = "MobHereticWormSegment";
    private const int    SegmentCount     = 20;

    public override void Initialize()
    {
        base.Initialize();
        // Запускаемся после SharedMoverController, чтобы перекрывать его SetLocalRotation.
        UpdatesAfter.Add(typeof(SharedMoverController));
        SubscribeLocalEvent<HereticComponent,     HereticShedHumanFormActionEvent>(OnShedForm);
        SubscribeLocalEvent<HereticWormComponent, HereticShedHumanFormActionEvent>(OnRevertForm);
        SubscribeLocalEvent<HereticWormComponent, MobStateChangedEvent>(OnWormDied);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticWormComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var worm, out var xform))
        {
            if (_mobState.IsIncapacitated(uid))
                continue;

            var currentPos = xform.Coordinates;
            var spacingSquared = worm.SegmentSpacing * worm.SegmentSpacing;

            // Добавляем новую точку пути ТОЛЬКО если голова сместилась на SegmentSpacing.
            // При остановке новых точек нет → сегменты остаются на месте.
            if (worm.PathPoints.Count == 0 ||
                (currentPos.EntityId == worm.PathPoints[0].EntityId &&
                 (currentPos.Position - worm.PathPoints[0].Position).LengthSquared() >= spacingSquared))
            {
                worm.PathPoints.Insert(0, currentPos);
                while (worm.PathPoints.Count > worm.Segments.Count + 2)
                    worm.PathPoints.RemoveAt(worm.PathPoints.Count - 1);
            }

            // t = прогресс головы от PathPoints[0] к следующей точке (0..1).
            // Используется для плавной интерполяции сегментов каждый тик.
            var t = 0f;
            if (worm.PathPoints.Count >= 1 && currentPos.EntityId == worm.PathPoints[0].EntityId)
            {
                var d = (currentPos.Position - worm.PathPoints[0].Position).Length();
                t = Math.Clamp(d / worm.SegmentSpacing, 0f, 1f);

                var headDir = currentPos.Position - worm.PathPoints[0].Position;
                if (headDir.LengthSquared() > 0.0001f)
                    worm.LastHeadAngle = new Angle(Math.Atan2(headDir.Y, headDir.X) + Math.PI / 2);
            }
            // Всегда устанавливаем поворот головы — перекрываем SharedMoverController и боевой режим.
            _transform.SetLocalRotation(uid, worm.LastHeadAngle);

            // Сегмент[i] интерполируется между PathPoints[i+1] и PathPoints[i].
            // При t=0: стоит на PathPoints[i+1]; при t=1: сдвинулся к PathPoints[i].
            // Расстояние голова→seg[0] постоянно = SegmentSpacing — нет «прыжков».
            for (var i = 0; i < worm.Segments.Count; i++)
            {
                var idxBack  = i + 1; // дальше от головы
                var idxFront = i;     // ближе к голове

                if (idxBack >= worm.PathPoints.Count)
                    break;

                var seg = worm.Segments[i];
                if (!Exists(seg))
                    continue;

                var ptBack  = worm.PathPoints[idxBack];
                var ptFront = worm.PathPoints[idxFront];

                EntityCoordinates segPos;
                if (ptBack.EntityId == ptFront.EntityId)
                {
                    var interp = Vector2.Lerp(ptBack.Position, ptFront.Position, t);
                    segPos = new EntityCoordinates(ptBack.EntityId, interp);
                }
                else
                {
                    segPos = ptBack;
                }
                _transform.SetCoordinates(seg, segPos);

                // Поворот сегмента: смотрит от ptBack к ptFront (в сторону головы).
                // -π/2 — коррекция ориентации спрайта сегмента (сдвиг на 90° по часовой).
                if (ptBack.EntityId == ptFront.EntityId)
                {
                    var dir = ptFront.Position - ptBack.Position;
                    if (dir.LengthSquared() > 0.0001f)
                        _transform.SetLocalRotation(seg, new Angle(Math.Atan2(dir.Y, dir.X) - Math.PI / 2));
                }
            }
        }
    }

    private void OnShedForm(EntityUid uid, HereticComponent _, HereticShedHumanFormActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        TransformToWorm(uid);
    }

    private void OnRevertForm(EntityUid uid, HereticWormComponent worm, HereticShedHumanFormActionEvent args)
    {
        if (args.Handled) return;
        args.Handled = true;
        RevertToHuman(uid, worm);
    }

    private void OnWormDied(EntityUid uid, HereticWormComponent worm, MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead)
            return;
        RevertToHuman(uid, worm);
    }

    private void TransformToWorm(EntityUid heretic)
    {
        if (!_mind.TryGetMind(heretic, out var mindId, out _))
            return;

        var coords = Transform(heretic).Coordinates;

        var wormUid  = Spawn(WormProto, coords);
        var wormComp = EnsureComp<HereticWormComponent>(wormUid);
        wormComp.OriginalBody = heretic;

        // PathPoints[0] = стартовая позиция головы (точка отсчёта).
        // PathPoints[h] = h * SegmentSpacing к югу — сегменты выстраиваются в линию.
        for (var h = 0; h <= SegmentCount; h++)
            wormComp.PathPoints.Add(coords.Offset(new Vector2(0f, -h * wormComp.SegmentSpacing)));

        // seg[i] → PathPoints[i+1]: спавним в PathPoints[1..SegmentCount].
        for (var i = 0; i < SegmentCount; i++)
        {
            var seg = Spawn(WormSegmentProto, wormComp.PathPoints[i + 1]);
            EnsureComp<HereticWormSegmentComponent>(seg);
            wormComp.Segments.Add(seg);
        }

        _transform.DetachParentToNull(heretic, Transform(heretic));
        _mind.TransferTo(mindId, wormUid, ghostCheckOverride: true);

        if (TryComp<HereticComponent>(heretic, out var hComp))
        {
            foreach (var actionEnt in hComp.GrantedActions)
            {
                if (!Exists(actionEnt))
                    continue;
                _actions.RemoveAction(new Entity<ActionComponent?>(actionEnt, null));
                _actions.AddActionDirect(new Entity<ActionsComponent?>(wormUid, null), new Entity<ActionComponent?>(actionEnt, null));
            }
        }

        _actions.AddAction(wormUid, ShedHumanFormProto);
        _popup.PopupEntity(Loc.GetString("heretic-worm-transform"), wormUid, wormUid, PopupType.Large);
    }

    private void RevertToHuman(EntityUid wormUid, HereticWormComponent worm)
    {
        if (!worm.OriginalBody.HasValue)
            return;

        var heretic = worm.OriginalBody.Value;
        worm.OriginalBody = null;

        if (!Exists(heretic))
            return;

        if (!_mind.TryGetMind(wormUid, out var mindId, out _))
            return;

        var wormCoords = Transform(wormUid).Coordinates;

        foreach (var seg in worm.Segments)
        {
            if (Exists(seg))
                QueueDel(seg);
        }
        worm.Segments.Clear();
        worm.PathPoints.Clear();

        _transform.SetCoordinates(heretic, wormCoords);
        _mind.TransferTo(mindId, heretic, ghostCheckOverride: true);

        if (TryComp<HereticComponent>(heretic, out var hComp))
        {
            foreach (var actionEnt in hComp.GrantedActions)
            {
                if (!Exists(actionEnt))
                    continue;
                _actions.RemoveAction(new Entity<ActionComponent?>(actionEnt, null));
                _actions.AddActionDirect(new Entity<ActionsComponent?>(heretic, null), new Entity<ActionComponent?>(actionEnt, null));
            }
        }

        QueueDel(wormUid);

        _popup.PopupEntity(Loc.GetString("heretic-worm-revert"), heretic, heretic, PopupType.Large);
    }
}
