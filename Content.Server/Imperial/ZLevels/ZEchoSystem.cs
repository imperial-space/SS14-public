using System.Numerics;
using Content.Server.Explosion.EntitySystems;
using Content.Shared.Explosion.Components;
using Content.Shared.Imperial.ZLevels;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.ZLevels;

/// <summary>
/// Что слышно и чувствуется через этажи (SS13 multi-z):
/// звук рядом с проёмом (открытое пространство, пустота, решётка, стекло) тише слышен этажом выше и ниже;
/// взрыв бьёт и по соседним этажам — слабее с каждым этажом, не дальше двух.
/// </summary>
public sealed class ZEchoSystem : EntitySystem
{
    [Dependency] private readonly ZLevelSystem _z = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ISharedPlayerManager _players = default!;

    /// <summary>На сколько тише звук на соседнем этаже.</summary>
    private const float EchoVolume = -6f;
    /// <summary>Как далеко от звука ищется проём.</summary>
    private const int OpenRange = 2;
    /// <summary>Дальше этого от звука на том этаже никого нет — не повторяем.</summary>
    private const float ListenerRange = 20f;
    /// <summary>Доля силы взрыва, доходящая до соседнего этажа.</summary>
    private const float ExplosionFalloff = 0.45f;
    private const int ExplosionDepth = 2;

    private readonly HashSet<EntityUid> _seenAudio = new();
    private readonly HashSet<EntityUid> _echoes = new();
    private readonly HashSet<EntityUid> _seenExplosions = new();
    /// <summary>Взрывы, которые поставили мы сами: куда они идут (вверх/вниз) и на каком они этаже от исходного.</summary>
    private readonly Dictionary<(MapId, Vector2i), (bool Up, int Depth)> _propagated = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        UpdateAudio();
        UpdateExplosions();
    }

    // ---------- Звук ----------

    private void UpdateAudio()
    {
        _seenAudio.RemoveWhere(e => TerminatingOrDeleted(e));
        _echoes.RemoveWhere(e => TerminatingOrDeleted(e));
        var query = EntityQueryEnumerator<AudioComponent, TransformComponent>();
        var fresh = new List<(EntityUid, AudioComponent, MapCoordinates)>();
        while (query.MoveNext(out var uid, out var audio, out var xform))
        {
            if (!_seenAudio.Add(uid) || _echoes.Contains(uid) || audio.Global || string.IsNullOrEmpty(audio.FileName))
                continue;
            if (xform.MapUid is not { } map || !HasComp<ZLevelMapComponent>(map))
                continue;
            fresh.Add((uid, audio, _xform.GetMapCoordinates(uid, xform)));
        }

        foreach (var (_, audio, coords) in fresh)
        {
            foreach (var up in new[] { true, false })
            {
                if (_z.GetStep(coords, up) is not { } target || !HasListener(target))
                    continue;
                // Проём ищем на этаже, сквозь который идёт звук: снизу вверх — над источником, сверху вниз — у источника.
                if (!HasOpenNear(up ? target : coords))
                    continue;
                if (!_map.TryGetMap(target.MapId, out var targetMap))
                    continue;
                var baseParams = audio.Params;
                var param = baseParams.WithVolume(baseParams.Volume + EchoVolume);
                var echo = _audio.PlayPvs(new ResolvedPathSpecifier(audio.FileName), _xform.ToCoordinates(targetMap.Value, target), param);
                if (echo != null)
                {
                    _echoes.Add(echo.Value.Entity);
                    _seenAudio.Add(echo.Value.Entity);
                }
            }
        }
    }

    private bool HasOpenNear(MapCoordinates coords)
    {
        for (var dx = -OpenRange; dx <= OpenRange; dx++)
        for (var dy = -OpenRange; dy <= OpenRange; dy++)
        {
            if (_z.IsTransparent(new MapCoordinates(coords.Position + new Vector2(dx, dy), coords.MapId)))
                return true;
        }
        return false;
    }

    private bool HasListener(MapCoordinates coords)
    {
        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is not { } ent)
                continue;
            var pos = _xform.GetMapCoordinates(ent);
            if (pos.MapId == coords.MapId && (pos.Position - coords.Position).LengthSquared() <= ListenerRange * ListenerRange)
                return true;
        }
        return false;
    }

    // ---------- Взрывы ----------

    private void UpdateExplosions()
    {
        _seenExplosions.RemoveWhere(e => TerminatingOrDeleted(e));
        var query = EntityQueryEnumerator<ExplosionVisualsComponent>();
        var fresh = new List<ExplosionVisualsComponent>();
        while (query.MoveNext(out var uid, out var visuals))
        {
            if (_seenExplosions.Add(uid))
                fresh.Add(visuals);
        }

        foreach (var visuals in fresh)
        {
            var epicenter = visuals.Epicenter;
            if (!_map.TryGetMap(epicenter.MapId, out var map) || !HasComp<ZLevelMapComponent>(map) || visuals.Intensity.Count == 0)
                continue;

            var key = (epicenter.MapId, (Vector2i) epicenter.Position.Floored());
            var directions = new List<(bool Up, int Depth)>();
            if (_propagated.Remove(key, out var from))
            {
                if (from.Depth < ExplosionDepth)
                    directions.Add((from.Up, from.Depth + 1));
            }
            else
            {
                directions.Add((true, 1));
                directions.Add((false, 1));
            }

            var total = TotalIntensity(visuals) * ExplosionFalloff;
            if (total < 1f)
                continue;
            var max = visuals.Intensity[0];
            var slope = visuals.Intensity.Count > 1 ? Math.Max(0.5f, visuals.Intensity[0] - visuals.Intensity[1]) : Math.Max(0.5f, max);

            foreach (var (up, depth) in directions)
            {
                if (_z.GetStep(epicenter, up) is not { } target)
                    continue;
                _propagated[(target.MapId, (Vector2i) target.Position.Floored())] = (up, depth);
                _explosion.QueueExplosion(target, visuals.ExplosionType, total, slope, max * ExplosionFalloff, null);
            }
        }
    }

    /// <summary>Полная сила взрыва: сумма силы каждой волны на число клеток в ней.</summary>
    private static float TotalIntensity(ExplosionVisualsComponent visuals)
    {
        var counts = new int[visuals.Intensity.Count];
        if (visuals.SpaceTiles != null)
        {
            foreach (var (i, tiles) in visuals.SpaceTiles)
            {
                if (i < counts.Length)
                    counts[i] += tiles.Count;
            }
        }
        foreach (var grid in visuals.Tiles.Values)
        {
            foreach (var (i, tiles) in grid)
            {
                if (i < counts.Length)
                    counts[i] += tiles.Count;
            }
        }
        var total = 0f;
        for (var i = 0; i < counts.Length; i++)
            total += visuals.Intensity[i] * counts[i];
        return total;
    }
}
