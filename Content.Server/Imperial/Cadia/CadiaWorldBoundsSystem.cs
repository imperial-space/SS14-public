using Content.Server.Popups;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Cadia;

/// <summary>
/// Край поля Кадии. Поле обрамляют неразрушимые скалы Кадийских врат, за ними стеной стоит буран. Если кто-то всё же
/// оказался за кромкой скал или провалился в пустоту на нижнем этаже (пробитый взрывом пол катакомб), буран сбивает его с пути
/// и возвращает туда, где он в последний раз стоял твёрдо.
/// </summary>
public sealed class CadiaWorldBoundsSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private static readonly TimeSpan CheckRate = TimeSpan.FromSeconds(0.5);
    /// <summary>Сколько клеток от края поля занимают скалы: за ними стоять нельзя.</summary>
    private const float Border = 2.5f;

    private TimeSpan _next;
    private readonly Dictionary<EntityUid, EntityCoordinates> _safe = new();
    private readonly Dictionary<EntityUid, TimeSpan> _nextPopup = new();
    private static readonly TimeSpan PopupDelay = TimeSpan.FromSeconds(3);

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;
        if (now < _next)
            return;
        _next = now + CheckRate;

        var mobs = EntityQueryEnumerator<MobStateComponent, TransformComponent>();
        var gone = new List<EntityUid>();
        foreach (var uid in _safe.Keys)
        {
            if (TerminatingOrDeleted(uid))
                gone.Add(uid);
        }
        foreach (var uid in gone)
        {
            _safe.Remove(uid);
            _nextPopup.Remove(uid);
        }

        while (mobs.MoveNext(out var uid, out _, out var xform))
        {
            // Только тот, кто стоит на самой сетке поля: в кабине корабля и на борту — свои правила.
            if (xform.MapUid is not { } field || xform.GridUid != field || !TryComp<CadiaFieldBoundsComponent>(field, out var bounds) ||
                !TryComp<MapGridComponent>(field, out var grid))
                continue;

            if (Safe((field, grid), bounds.Bounds, CompOrNull<ZLevelMapComponent>(field), xform))
            {
                _safe[uid] = xform.Coordinates;
                continue;
            }
            if (!_safe.TryGetValue(uid, out var back) || _xform.GetMapId(back) != xform.MapID)
                continue;
            _xform.SetCoordinates(uid, back);
            if (!_nextPopup.TryGetValue(uid, out var next) || now >= next)
            {
                _nextPopup[uid] = now + PopupDelay;
                _popup.PopupEntity(Loc.GetString("cadia-bounds-push"), uid, uid, PopupType.MediumCaution);
            }
        }
    }

    /// <summary>Внутри скал и, на нижнем этаже, на твёрдом полу.</summary>
    private bool Safe(Entity<MapGridComponent> field, Box2 bounds, ZLevelMapComponent? z, TransformComponent xform)
    {
        var inner = bounds.Enlarged(-Border);
        var pos = xform.LocalPosition;
        if (!inner.Contains(pos))
            return false;
        if (z?.Below != null)
            return true;
        var tile = _map.GetTileRef(field, field.Comp, xform.Coordinates);
        return !tile.Tile.IsEmpty;
    }
}
