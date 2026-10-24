using System.Linq;
using System.Numerics;
using Content.Shared.Explosion.Components;
using Content.Shared.Imperial.Fishing;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Взрывная рыбалка (fishing_spot/explosive_fishing → spawn_reward_from_explosion): взрыв над местом ловли
/// выбрасывает мёртвый улов. У некоторых источников частые взрывы дают всё меньше (EXPLOSIVE_MALUS).
/// </summary>
public sealed class ImperialExplosiveFishingSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly ImperialFishingSpotSystem _spots = default!;
    [Dependency] private readonly ImperialFishSourceSystem _sources = default!;
    [Dependency] private readonly ImperialFishSystem _fish = default!;

    private const float MalusExponent = 0.55f;
    private const float RecoveryRate = 0.18f;

    /// <summary>Не больше стольких мест ловли за один взрыв, чтобы большой взрыв над озером не завалил всё рыбой.</summary>
    private const int MaxSpotsPerExplosion = 40;

    private readonly List<EntityUid> _pending = new();

    /// <summary>explosive_fishing_score по источникам.</summary>
    private readonly Dictionary<string, float> _scores = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ExplosionVisualsComponent, ComponentInit>(OnExplosionInit);
    }

    private void OnExplosionInit(Entity<ExplosionVisualsComponent> ent, ref ComponentInit args)
    {
        // Тайлы и сила заполняются после создания — читаем на следующем тике.
        _pending.Add(ent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var key in _scores.Keys.ToList())
        {
            _scores[key] -= RecoveryRate * frameTime;
            if (_scores[key] <= 0)
                _scores.Remove(key);
        }

        if (_pending.Count == 0)
            return;

        foreach (var boom in _pending.ToList())
        {
            if (TryComp<ExplosionVisualsComponent>(boom, out var visuals))
                ProcessExplosion(visuals);
        }

        _pending.Clear();
    }

    private void ProcessExplosion(ExplosionVisualsComponent visuals)
    {
        var processed = 0;
        var seen = new HashSet<EntityUid>();
        foreach (var (grid, iterations) in visuals.Tiles)
        {
            if (!TryComp<MapGridComponent>(grid, out var gridComp))
                continue;

            foreach (var (iteration, tiles) in iterations)
            {
                var intensity = iteration < visuals.Intensity.Count ? visuals.Intensity[iteration] : 1;
                var severity = intensity >= 10 ? 3 : intensity >= 4 ? 2 : 1;
                foreach (var tile in tiles)
                {
                    foreach (var uid in _lookup.GetLocalEntitiesIntersecting(grid, tile, 0f, gridComp: gridComp))
                    {
                        if (processed >= MaxSpotsPerExplosion || !seen.Add(uid))
                            continue;

                        if (_spots.GetSource(uid) is not { } source)
                            continue;

                        processed++;
                        SpawnFromExplosion(source, uid, severity);
                    }
                }
            }
        }

        // Тайлы вне гридов (вода и лава на планетах без грида, места ловли в космосе).
        if (visuals.SpaceTiles == null)
            return;

        foreach (var (iteration, tiles) in visuals.SpaceTiles)
        {
            var intensity = iteration < visuals.Intensity.Count ? visuals.Intensity[iteration] : 1;
            var severity = intensity >= 10 ? 3 : intensity >= 4 ? 2 : 1;
            foreach (var tile in tiles)
            {
                var local = (new Vector2(tile.X, tile.Y) + new Vector2(0.5f)) * visuals.SpaceTileSize;
                var world = Vector2.Transform(local, visuals.SpaceMatrix);
                foreach (var uid in _lookup.GetEntitiesInRange(new MapCoordinates(world, visuals.Epicenter.MapId), visuals.SpaceTileSize * 0.5f))
                {
                    if (processed >= MaxSpotsPerExplosion || !seen.Add(uid) || _spots.GetSource(uid) is not { } source)
                        continue;

                    processed++;
                    SpawnFromExplosion(source, uid, severity);
                }
            }
        }
    }

    /// <summary>spawn_reward_from_explosion().</summary>
    private void SpawnFromExplosion(ImperialFishSourcePrototype source, EntityUid location, int severity)
    {
        if (source.ExplosiveNone)
            return;

        var multiplier = 1f;
        if (source.ExplosiveMalus)
        {
            var score = _scores.GetValueOrDefault(source.ID);
            if (score <= 0)
            {
                _scores[source.ID] = 1;
            }
            else
            {
                score++;
                _scores[source.ID] = score;
                multiplier = MathF.Pow(score, -MalusExponent);
            }
        }

        var coords = Transform(location).Coordinates;
        for (var i = 1; i <= severity + 2; i++)
        {
            if (!_random.Prob(Math.Clamp((100f + 100f * severity) / i * multiplier / 100f, 0, 1)))
                continue;

            var table = _sources.GetTable(source);
            if (_sources.PickWeighted(table) is not { } reward)
                continue;

            if (_sources.SpawnReward(source, reward, coords) is not { } spawned)
                continue;

            if (TryComp<ImperialFishComponent>(spawned, out var fish))
                _fish.SetDead((spawned, fish), true, silent: true);

            _xform.SetCoordinates(spawned, coords.Offset(new Vector2(_random.NextFloat(-0.3f, 0.3f), _random.NextFloat(-0.3f, 0.3f))));
        }
    }
}
