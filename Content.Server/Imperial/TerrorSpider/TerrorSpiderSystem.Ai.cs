using Robust.Shared.Random;
using System.Linq;
using Content.Server.Atmos.Piping.Unary.Components;
using Content.Server.NPC.Systems;
using Content.Shared.Item;
using Content.Shared.Light.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Paper;
using Content.Shared.Popups;
using Content.Shared.Tools.Components;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>
/// terror_ai.dm: поведение ИИ без цели — вентиляция, лампы, паутина и действия типа.
/// Бой (выбор и преследование цели) ведёт HTN.
/// </summary>
public sealed partial class TerrorSpiderSystem
{
    [Dependency] private readonly NPCSteeringSystem _steering = default!;

    private const string VentContainer = "terror_vent_travel";

    /// <summary>spider_max_steps.</summary>
    private const int MaxSteps = 15;

    private static readonly TimeSpan BreakLightFrequency = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan SpinWebFrequency = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan CocoonObjectFrequency = TimeSpan.FromSeconds(120);

    private TimeSpan _nextAiTick;
    private readonly Dictionary<EntityUid, (EntityUid Vent, int Steps)> _pathToVent = new();
    private readonly Dictionary<EntityUid, int> _cocoonSteps = new();
    private readonly List<(TimeSpan At, EntityUid Spider, EntityUid Exit, EntityCoordinates Origin, bool Entered)> _ventTravels = new();

    private void UpdateAi(TimeSpan now)
    {
        UpdateVentTravel(now);

        if (now < _nextAiTick)
            return;

        _nextAiTick = now + LifeInterval;

        var query = EntityQueryEnumerator<TerrorSpiderComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var spider, out _))
        {
            if (_mobState.IsDead(uid) || HasComp<ActorComponent>(uid) || _container.IsEntityInContainer(uid) || spider.Busy)
                continue;

            if (HasTarget(uid))
                continue;

            AutomatedMovement((uid, spider), now);
        }
    }

    /// <summary>ListTargets(): живые не-пауки в радиусе зрения (10).</summary>
    private bool HasTarget(EntityUid spider)
    {
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(_transform.GetMapCoordinates(spider), 10))
        {
            if (mob.Owner == spider || _mobState.IsDead(mob) || HasComp<TerrorSpiderComponent>(mob) || HasComp<TerrorSpiderlingComponent>(mob))
                continue;
            if (_container.IsEntityInContainer(mob))
                continue;
            return true;
        }

        return false;
    }

    /// <summary>handle_automated_movement().</summary>
    private void AutomatedMovement(Entity<TerrorSpiderComponent> ent, TimeSpan now)
    {
        var spider = ent.Comp;
        var ventFrequency = RecentDeath(now) ? spider.VentcrawlCombatFrequency : spider.VentcrawlIdleFrequency;

        if (_pathToVent.TryGetValue(ent, out var path))
        {
            if (!Exists(path.Vent) || path.Steps > MaxSteps)
            {
                _pathToVent.Remove(ent);
                _steering.Unregister(ent);
            }
            else if (InRange(ent, path.Vent, 1.5f))
            {
                _pathToVent.Remove(ent);
                _steering.Unregister(ent);
                VentCrawlRandom(ent, path.Vent);
            }
            else
            {
                _pathToVent[ent] = (path.Vent, path.Steps + 1);
                _steering.Register(ent, Transform(path.Vent).Coordinates);
            }
        }
        else if (spider.AiBreakLights && now > spider.LastBreakLight + BreakLightFrequency)
        {
            spider.LastBreakLight = now;
            foreach (var light in _lookup.GetEntitiesInRange<PoweredLightComponent>(_transform.GetMapCoordinates(ent), 1.5f))
            {
                if (_lights.TryDestroyBulb(light, light.Comp))
                {
                    _popup.PopupEntity(Loc.GetString("terror-spider-smashes-light", ("spider", ent.Owner), ("light", light.Owner)), ent);
                    return;
                }
            }
        }
        else if (spider.AiSpinsWebs && spider.Web is { } web && now > spider.LastWeb + SpinWebFrequency)
        {
            spider.LastWeb = now;
            if (OnFloor(ent) && !IsSpace(ent) && WebAt(Transform(ent).Coordinates) == null)
            {
                Spawn(web, _transform.GetMoverCoordinates(ent));
                _popup.PopupEntity(Loc.GetString("terror-spider-puts-webs", ("spider", ent.Owner)), ent);
            }
        }
        else if (spider.AiVentcrawls && now > spider.LastVentcrawl + ventFrequency && _random.Prob(spider.IdleVentcrawlChance / 100f))
        {
            spider.LastVentcrawl = now;
            if (NearestVent(ent, 10, spider.AiVentBreaker) is { } vent)
                _pathToVent[ent] = (vent, 0);
        }
        else
        {
            SpecialAction(ent, now);
        }
    }

    private EntityUid? NearestVent(EntityUid spider, float range, bool allowWelded)
    {
        EntityUid? best = null;
        var bestDist = float.MaxValue;
        var pos = _transform.GetMapCoordinates(spider);
        foreach (var vent in _lookup.GetEntitiesInRange<GasVentPumpComponent>(pos, range))
        {
            if (!allowWelded && TryComp<WeldableComponent>(vent, out var weld) && weld.IsWelded)
                continue;
            var dist = (_transform.GetMapCoordinates(vent).Position - pos.Position).Length();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = vent;
            }
        }

        return best;
    }

    /// <summary>TSVentCrawlRandom(): нырнуть в трубы и вылезти из случайной вентиляции той же сети.</summary>
    private void VentCrawlRandom(Entity<TerrorSpiderComponent> ent, EntityUid entry)
    {
        if (ent.Comp.AiVentBreaker && TryComp<WeldableComponent>(entry, out var entryWeld) && entryWeld.IsWelded)
            SmashVent(ent, entry);

        var grid = Transform(entry).GridUid;
        var vents = new List<EntityUid>();
        var query = EntityQueryEnumerator<GasVentPumpComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out _, out var xform))
        {
            if (uid != entry && xform.GridUid == grid)
                vents.Add(uid);
        }

        if (vents.Count == 0)
            return;

        var exit = _random.Pick(vents);
        _popup.PopupEntity(Loc.GetString("terror-spider-scrambles-vents", ("spider", ent.Owner)), ent, PopupType.Medium);
        var delay = TimeSpan.FromSeconds(_random.Next(2, 7));
        _ventTravels.Add((_timing.CurTime + delay, ent.Owner, exit, Transform(ent).Coordinates, false));
    }

    private void UpdateVentTravel(TimeSpan now)
    {
        for (var i = _ventTravels.Count - 1; i >= 0; i--)
        {
            var travel = _ventTravels[i];
            if (now < travel.At)
                continue;

            _ventTravels.RemoveAt(i);
            if (!Exists(travel.Spider) || _mobState.IsDead(travel.Spider))
                continue;

            if (!travel.Entered)
            {
                // Паук в трубах: прячем его в вентиляции выхода на время пути.
                if (!Exists(travel.Exit))
                    continue;
                var container = _container.EnsureContainer<Container>(travel.Exit, VentContainer);
                if (!_container.Insert(travel.Spider, container))
                    continue;

                var distance = (_transform.GetMapCoordinates(travel.Exit).Position - travel.Origin.Position).Length();
                var travelTime = TimeSpan.FromSeconds(Math.Max(1, distance / 10f));
                _ventTravels.Add((now + travelTime, travel.Spider, travel.Exit, travel.Origin, true));
                continue;
            }

            var breaker = TryComp<TerrorSpiderComponent>(travel.Spider, out var spider) && spider.AiVentBreaker;
            var welded = TryComp<WeldableComponent>(travel.Exit, out var weld) && weld.IsWelded;
            _container.TryRemoveFromContainer(travel.Spider, true);

            if (!Exists(travel.Exit) || welded && !breaker)
            {
                _transform.SetCoordinates(travel.Spider, travel.Origin);
                continue;
            }

            if (welded)
            {
                SmashVent(travel.Spider, travel.Exit);
                _audio.PlayPvs(PrySound, travel.Exit);
            }

            _transform.SetCoordinates(travel.Spider, Transform(travel.Exit).Coordinates);
            _transform.AttachToGridOrMap(travel.Spider);
        }
    }

    /// <summary>spider_special_action() по типам.</summary>
    private void SpecialAction(Entity<TerrorSpiderComponent> ent, TimeSpan now)
    {
        if (TryComp<TerrorSpiderGrayComponent>(ent, out var gray))
        {
            if (_random.Prob(gray.MasswebChance / 100f) && ent.Comp.Web is { } web)
                MassWeb(ent, web);
            return;
        }

        if (TryComp<TerrorSpiderGreenComponent>(ent, out var green))
        {
            if (ent.Comp.CocoonTarget != null)
                HandleCocoonTarget(ent);
            else if (ent.Comp.Fed >= green.FeedingsToLay)
                AiLayGreenEggs((ent, ent.Comp, green));
            else if (now > ent.Comp.LastCocoonObject + CocoonObjectFrequency)
                SeekCocoonTarget(ent, now);
            return;
        }

        if (TryComp<TerrorSpiderQueenComponent>(ent, out var queen))
            QueenAi((ent, ent.Comp, queen), now);
    }

    /// <summary>gray/spider_special_action(): паутина на всех свободных тайлах в радиусе 2.</summary>
    private void MassWeb(EntityUid spider, Robust.Shared.Prototypes.EntProtoId web)
    {
        var xform = Transform(spider);
        if (xform.GridUid is not { } gridUid || !TryComp<Robust.Shared.Map.Components.MapGridComponent>(gridUid, out var grid))
            return;

        var center = _map.TileIndicesFor(gridUid, grid, xform.Coordinates);
        for (var x = -2; x <= 2; x++)
        {
            for (var y = -2; y <= 2; y++)
            {
                if (x == 0 && y == 0)
                    continue;
                var tile = center + new Vector2i(x, y);
                if (!_map.TryGetTileRef(gridUid, grid, tile, out var tileRef) || _turf.IsSpace(tileRef)
                    || _turf.IsTileBlocked(tileRef, Content.Shared.Physics.CollisionGroup.Impassable))
                    continue;
                var coords = _map.GridTileToLocal(gridUid, grid, tile);
                if (WebAt(coords) == null)
                    Spawn(web, coords);
            }
        }
    }

    [Dependency] private readonly Robust.Server.GameObjects.MapSystem _map = default!;

    /// <summary>handle_cocoon_target().</summary>
    private void HandleCocoonTarget(Entity<TerrorSpiderComponent> ent)
    {
        if (ent.Comp.CocoonTarget is not { } target || !Exists(target))
        {
            ent.Comp.CocoonTarget = null;
            return;
        }

        if (InRange(ent, target, 1.5f))
        {
            _cocoonSteps.Remove(ent);
            _steering.Unregister(ent);
            DoWrap(ent);
            return;
        }

        var steps = _cocoonSteps.GetValueOrDefault(ent);
        if (steps > MaxSteps)
        {
            _cocoonSteps.Remove(ent);
            ent.Comp.CocoonTarget = null;
            _steering.Unregister(ent);
            return;
        }

        _cocoonSteps[ent] = steps + 1;
        _steering.Register(ent, Transform(target).Coordinates);
    }

    /// <summary>seek_cocoon_target(): труп в поле зрения, иначе любой незакреплённый предмет (кроме бумаги).</summary>
    private void SeekCocoonTarget(Entity<TerrorSpiderComponent> ent, TimeSpan now)
    {
        ent.Comp.LastCocoonObject = now;
        var pos = _transform.GetMapCoordinates(ent);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(pos, 10))
        {
            if (_mobState.IsDead(mob) && !HasComp<TerrorSpiderComponent>(mob) && !Transform(mob).Anchored
                && !_container.IsEntityInContainer(mob))
            {
                _cocoonSteps[ent] = 0;
                ent.Comp.CocoonTarget = mob;
                return;
            }
        }

        foreach (var item in _lookup.GetEntitiesInRange<ItemComponent>(pos, 10))
        {
            if (Transform(item).Anchored || _container.IsEntityInContainer(item) || HasComp<PaperComponent>(item))
                continue;
            _cocoonSteps[ent] = 0;
            ent.Comp.CocoonTarget = item;
            return;
        }
    }

    private void AiLayGreenEggs(Entity<TerrorSpiderComponent, TerrorSpiderGreenComponent> ent)
    {
        var types = GreenEggTypes(ent);
        LayGreenEggs(ent, _random.Pick(types));
    }

    /// <summary>queen/spider_special_action(): поиск места, гнездо, первые стражи, заполнение гнезда.</summary>
    private void QueenAi(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent, TimeSpan now)
    {
        var queen = ent.Comp2;
        if (queen.Empress)
            return;

        if (queen.Princess && queen.NestStep == 2)
        {
            // Упрощённая королева: по яйцу раз в nestfrequency, пока выводок не полон.
            if (now < queen.LastNestSetup + queen.NestFrequency)
                return;
            queen.LastNestSetup = now;
            if (NestIsFull(ent))
                return;
            queen.LastSpawn = now;
            DoLayTerrorEggs(ent, _random.Pick(StandardTypes), 1);
            UpdatePrincessStage(ent);
            return;
        }

        switch (queen.NestStep)
        {
            case 0:
            {
                var ok = true;
                var area = AreaName(ent).ToLowerInvariant();
                // Гнездо в коридоре экипаж найдёт сразу.
                if (area.Contains("hall") || area.Contains("коридор"))
                    ok = false;
                if (HasTarget(ent))
                    ok = false;

                var vent = NearestVent(ent, 10, false);
                if (vent == null)
                    ok = false;

                if (ok && vent != null)
                {
                    queen.NestVent = vent;
                    queen.NestStep = 1;
                    _popup.PopupEntity(Loc.GetString("terror-spider-queen-settles", ("spider", ent.Owner)), ent, PopupType.Medium);
                }
                else if (vent is { } entry)
                {
                    if (!_pathToVent.ContainsKey(ent))
                    {
                        _popup.PopupEntity(Loc.GetString("terror-spider-queen-seeks-nest", ("spider", ent.Owner)), ent);
                        _pathToVent[ent] = (entry, 0);
                    }
                }
                else
                {
                    queen.NestStep = -1;
                    Log.Warning($"{ToPrettyString(ent)} spawned in an area without a vent; its AI has been deactivated.");
                }
                break;
            }
            case 1:
                if (now > queen.LastNestSetup + queen.NestFrequency)
                {
                    queen.LastNestSetup = now;
                    queen.NestStep = 2;
                    if (queen.Princess)
                        break;
                    NestMode(ent);
                }
                break;
            case 2:
                if (now > queen.LastNestSetup + queen.NestFrequency)
                {
                    queen.LastNestSetup = now;
                    queen.LastSpawn = now;
                    DoLayTerrorEggs(ent, Purple, 2);
                    DoLayTerrorEggs(ent, White, 2);
                    DoLayTerrorEggs(ent, Brown, 2);
                    queen.NestStep = 3;
                }
                break;
            case 3:
                if (now > queen.LastSpawn + queen.SpawnFrequency && _random.Prob(0.2f))
                {
                    if (NestIsFull(ent))
                    {
                        queen.NestStep = 4;
                    }
                    else
                    {
                        queen.LastSpawn = now;
                        var left = Math.Clamp(queen.MaxPerNest - CountSpiders(), 1, 10);
                        DoLayTerrorEggs(ent, _random.Pick(StandardTypes), left);
                    }
                }
                break;
            case 4:
                if (now > queen.LastSpawn + queen.SpawnFrequency && _random.Prob(0.2f) && !NestIsFull(ent))
                    queen.NestStep = 5;
                break;
            case 5:
                if (now > queen.LastSpawn + queen.SpawnFrequency && _random.Prob(0.2f))
                {
                    if (NestIsFull(ent))
                    {
                        queen.NestStep = 4;
                        break;
                    }

                    queen.LastSpawn = now;
                    var counts = CountSpidersDetailed(ent, false);
                    if (counts.GetValueOrDefault(Purple) < 4)
                        DoLayTerrorEggs(ent, Purple, 2);
                    else if (counts.GetValueOrDefault(White) < 2)
                        DoLayTerrorEggs(ent, White, 2);
                    else if (counts.GetValueOrDefault(Brown) < 4)
                        DoLayTerrorEggs(ent, Brown, 4);
                    else
                        DoLayTerrorEggs(ent, _random.Pick(StandardTypes), 5);
                }
                break;
        }
    }

    /// <summary>ai_nest_is_full().</summary>
    private bool NestIsFull(Entity<TerrorSpiderComponent, TerrorSpiderQueenComponent> ent)
    {
        if (ent.Comp2.Princess)
            return CountSpidersDetailed(ent, true)["all"] >= ent.Comp2.MaxChildren;
        return CountSpiders() >= ent.Comp2.MaxPerNest;
    }
}
