using System.Linq;
using System.Numerics;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Imperial.Wizard.Spells;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Emp;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Maps;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Stunnable;
using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server.Imperial.Wizard.GrandRitual;

/// <summary>Тема измерения (/datum/dimension_theme): пол и стены.</summary>
public readonly record struct ImperialDimensionTheme(string Floor, EntProtoId? Wall);

/// <summary>/datum/grand_side_effect: местные последствия каждой руны после первой.</summary>
public sealed partial class ImperialGrandRitualSystem
{
    [Dependency] private readonly ImperialWizardSpellsSystem _spells = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SmokeSystem _smoke = default!;
    [Dependency] private readonly PuddleSystem _puddle = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly MobStateSystem _mobs = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    private static readonly ProtoId<TagPrototype> WallTag = "Wall";
    private static readonly ProtoId<ReagentPrototype> SpaceLube = "SpaceLube";
    private static readonly ProtoId<ReagentPrototype> BloodReagent = "Blood";
    private static readonly ProtoId<ReagentPrototype>[] SmokeReagents = { "Water", "Blood", "Nutriment", "Plasma", "Ethanol", "SpaceDrugs" };

    /// <summary>subtypesof(/datum/dimension_theme) — то, что есть в SS14.</summary>
    public static readonly ImperialDimensionTheme[] Themes =
    {
        new("FloorGold", "WallGold"),
        new("FloorFlesh", "WallMeat"),
        new("FloorGrass", "WallWood"),
        new("FloorWood", "WallWood"),
        new("FloorPlasmaMarble", "WallPlasma"),
        new("FloorUraniumMarble", "WallUranium"),
        new("FloorSilver", "WallSilver"),
        new("FloorClown", "WallClown"),
        new("FloorGlass", null),
    };

    /// <summary>create_anomalies: permitted_anomalies и сколько каждой можно создать.</summary>
    private static readonly (EntProtoId Proto, int Max)[] PermittedAnomalies =
    {
        ("AnomalyFlesh", 1),
        ("AnomalyShadow", 2),
        ("AnomalyGravity", 2),
        ("AnomalyElectricity", 3),
    };

    /// <summary>spawn_delayed_mobs: permitted_mobs.</summary>
    private static readonly EntProtoId[] PermittedMobs =
    {
        "MobCarp", "MobTomatoKiller", "MobSkeletonPirate", "MobCarpHolo", "MobAdultSlimesGreenAngry",
    };

    /// <summary>get_random_food().</summary>
    private static readonly EntProtoId[] RainFood =
    {
        "FoodPizzaMargherita", "FoodBurgerBig", "FoodDonutPink", "FoodBreadPlain", "FoodCakePlain", "FoodBanana",
        "FoodApple", "FoodCheeseSlice", "FoodPieApple", "FoodBurgerCheese", "FoodBakedCookie",
    };

    /// <summary>subtypesof(/obj/item/instrument).</summary>
    private static readonly EntProtoId[] Instruments =
    {
        "ViolinInstrument", "TrumpetInstrument", "AcousticGuitarInstrument", "SaxophoneInstrument", "AccordionInstrument",
        "TromboneInstrument", "HarmonicaInstrument", "BanjoInstrument",
    };

    private static float Lerp(float a, float b, int potency)
    {
        return a + (b - a) * potency / FinaleCount;
    }

    /// <summary>trigger_side_effects(): кроме первой руны.</summary>
    private void TriggerSideEffects(int potency, EntityCoordinates location, EntityUid invoker)
    {
        if (potency == 0)
            return;

        var effects = SideEffects(potency, location, true);
        var (name, effect) = _random.Pick(effects.ToList());
        Log.Info($"Побочный эффект Великого ритуала: {name}");
        effect();
    }

    /// <summary>can_trigger(): доступные побочные эффекты по имени.</summary>
    public Dictionary<string, Action> SideEffects(int potency, EntityCoordinates location, bool checkConditions)
    {
        var effects = new Dictionary<string, Action>
        {
            ["scramble_turfs"] = () => ScrambleTurfs(potency, location),
            ["transmogrify_area"] = () => TransmogrifyArea(potency, location),
            ["create_anomalies"] = () => CreateAnomalies(potency, location),
            ["emp"] = () => Emp(potency, location),
            ["slippery"] = () => Slippery(potency, location),
            ["smoke"] = () => Smoke(potency, location),
            ["gore"] = () => Gore(location),
            ["create_food"] = () => FoodRain(potency, location),
            ["spawn_delayed_mobs"] = () => DelayedMobs(potency, location),
            ["orchestra"] = () => Orchestra(potency, location),
        };

        if (!checkConditions || _lookup.GetEntitiesInRange<MobStateComponent>(location, 5f).Count >= 2)
            effects["translocate"] = () => Translocate(location);

        if (!checkConditions || SummonCandidates(location).Count > 0)
            effects["summon_crewmate"] = () => SummonCrewmate(location);

        return effects;
    }

    /// <summary>scramble_turfs: искажение пространства-времени на 10–15 секунд.</summary>
    private void ScrambleTurfs(int potency, EntityCoordinates location)
    {
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/timeparadox2.ogg"), location);
        _spells.CreateSpacetime(location, 7f, TimeSpan.FromSeconds(Lerp(10, 15, potency)));
    }

    /// <summary>transmogrify_area: местность вокруг превращается в случайное измерение.</summary>
    private void TransmogrifyArea(int potency, EntityCoordinates location)
    {
        var range = (int) Math.Round(Lerp(2, 4, potency));
        TransformAround(_random.Pick(Themes), location, range, TimeSpan.FromSeconds(0.5));
    }

    /// <summary>Превратить клетки кольцами от центра: каждое кольцо — через <paramref name="step"/>.</summary>
    public void TransformAround(ImperialDimensionTheme theme, EntityCoordinates center, int range, TimeSpan step)
    {
        if (_transform.GetGrid(center) is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        var origin = _map.TileIndicesFor(gridUid, grid, center);
        var rings = new Dictionary<int, List<Vector2i>>();
        for (var x = -range; x <= range; x++)
        {
            for (var y = -range; y <= range; y++)
            {
                var dist = Math.Max(Math.Abs(x), Math.Abs(y));
                if (!rings.TryGetValue(dist, out var ring))
                    rings[dist] = ring = new List<Vector2i>();
                ring.Add(origin + new Vector2i(x, y));
            }
        }

        foreach (var (dist, tiles) in rings)
        {
            Schedule(step * dist, () =>
            {
                if (TerminatingOrDeleted(gridUid))
                    return;

                foreach (var tile in tiles)
                {
                    ApplyTheme(theme, gridUid, grid, tile);
                }
            });
        }
    }

    /// <summary>apply_theme(): пол и стены клетки.</summary>
    public void ApplyTheme(ImperialDimensionTheme theme, EntityUid gridUid, MapGridComponent grid, Vector2i indices)
    {
        var tileRef = _map.GetTileRef(gridUid, grid, indices);
        if (tileRef.Tile.IsEmpty || _turf.IsSpace(tileRef))
            return;

        if (theme.Wall is { } wall)
        {
            foreach (var anchored in _map.GetAnchoredEntities(gridUid, grid, indices).ToList())
            {
                if (!_tag.HasTag(anchored, WallTag) || MetaData(anchored).EntityPrototype?.ID == wall.Id)
                    continue;

                var coords = Transform(anchored).Coordinates;
                Del(anchored);
                Spawn(wall, coords);
            }
        }

        var def = (ContentTileDefinition) _tileDefs[theme.Floor];
        if (tileRef.Tile.TypeId != def.TileId)
            _map.SetTile(gridUid, grid, indices, new Tile(def.TileId));
    }

    /// <summary>create_anomalies: 1–4 безопасных аномалии на 15–30 секунд, без ядер.</summary>
    private void CreateAnomalies(int potency, EntityCoordinates location)
    {
        var toCreate = Math.Clamp((int) Math.Round(_random.Next(1, 5) + Lerp(-1, 1, potency)), 1, 4);
        var left = PermittedAnomalies.ToDictionary(a => a.Proto, a => a.Max);
        var tiles = _spells.FreeTilesAround(location, 4f, true);
        while (toCreate > 0 && tiles.Count > 0 && left.Values.Any(v => v > 0))
        {
            var proto = _random.Pick(left.Where(p => p.Value > 0).Select(p => p.Key).ToList());
            left[proto]--;
            var anomaly = Spawn(proto, _random.Pick(tiles));
            EnsureComp<TimedDespawnComponent>(anomaly).Lifetime = _random.NextFloat(15f, 30f);
            toCreate--;
        }
    }

    /// <summary>emp: empulse вокруг руны.</summary>
    private void Emp(int potency, EntityCoordinates location)
    {
        _emp.EmpPulse(_transform.ToMapCoordinates(location), Lerp(3, 6, potency), 50000f, TimeSpan.FromSeconds(Lerp(10, 30, potency)));
    }

    private List<EntityUid> SummonCandidates(EntityCoordinates location)
    {
        var near = _lookup.GetEntitiesInRange<HumanoidProfileComponent>(location, AreaRadius).Select(e => e.Owner).ToHashSet();
        var result = new List<EntityUid>();
        var query = EntityQueryEnumerator<HumanoidProfileComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (near.Contains(uid) || !_mind.TryGetMind(uid, out _, out _) || _mobs.IsDead(uid) || _station.GetOwningStation(uid) == null)
                continue;

            result.Add(uid);
        }

        return result;
    }

    /// <summary>translocate: существа рядом меняются местами.</summary>
    private void Translocate(EntityCoordinates location)
    {
        var mobs = _lookup.GetEntitiesInRange<MobStateComponent>(location, 5f).Select(m => m.Owner).ToList();
        if (mobs.Count < 2)
            return;

        var positions = mobs.Select(m => _transform.GetMapCoordinates(m)).ToList();
        _random.Shuffle(positions);
        for (var i = 0; i < mobs.Count; i++)
        {
            Spawn("WizardSmoke", _transform.GetMapCoordinates(mobs[i]));
            _transform.SetMapCoordinates(mobs[i], positions[i]);
        }
    }

    /// <summary>slippery: пена из космической смазки.</summary>
    private void Slippery(int potency, EntityCoordinates location)
    {
        var foam = Spawn("Foam", _transform.ToMapCoordinates(location));
        var solution = new Solution();
        solution.AddReagent(SpaceLube, 100);
        var range = Lerp(2, 4, potency);
        _smoke.StartSmoke(foam, solution, 15f, (int) Math.Round(range * range * 2));
    }

    /// <summary>summon_crewmate: кого-то с другого конца станции поднимает в воздух и переносит к руне.</summary>
    private void SummonCrewmate(EntityCoordinates location)
    {
        var candidates = SummonCandidates(location);
        if (candidates.Count == 0)
            return;

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/lightning_chargeup.ogg"), location);
        var victim = _random.Pick(candidates);
        var tiles = _spells.FreeTilesAround(location, 1.5f, true);
        var landing = tiles.Count > 0 ? _random.Pick(tiles) : location;
        Spawn("ImperialGrandRitualTeleportMark", landing);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/repulse.ogg"), victim);
        _stun.TryAddStunDuration(victim, TimeSpan.FromSeconds(9));
        _popup.PopupEntity(Loc.GetString("imperial-grand-ritual-summon-float"), victim, victim, PopupType.LargeCaution);
        Schedule(TimeSpan.FromSeconds(9), () =>
        {
            if (TerminatingOrDeleted(victim))
                return;

            Spawn("WizardSmoke", _transform.GetMapCoordinates(victim));
            _transform.SetCoordinates(victim, landing);
            _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/teleport_app.ogg"), victim);
        });
    }

    /// <summary>smoke: облако цветного дыма.</summary>
    private void Smoke(int potency, EntityCoordinates location)
    {
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/smoke.ogg"), location);
        var smoke = Spawn("Smoke", _transform.ToMapCoordinates(location));
        var solution = new Solution();
        solution.AddReagent(_random.Pick(SmokeReagents), 10);
        var range = Lerp(2, 4, potency);
        _smoke.StartSmoke(smoke, solution, 10f, (int) Math.Round(range * range * 2));
    }

    /// <summary>gore: кровавое месиво.</summary>
    private void Gore(EntityCoordinates location)
    {
        var tiles = _spells.FreeTilesAround(location, 2f, true);
        for (var i = 0; i < 4 && tiles.Count > 0; i++)
        {
            var solution = new Solution();
            solution.AddReagent(BloodReagent, 40);
            _puddle.TrySpillAt(_random.Pick(tiles), solution, out _, sound: false);
        }
    }

    /// <summary>create_food: с неба 10–30 секунд падает еда.</summary>
    private void FoodRain(int potency, EntityCoordinates location)
    {
        var seconds = (int) Lerp(10, 30, potency);
        for (var s = 0; s < seconds; s++)
        {
            Schedule(TimeSpan.FromSeconds(s), () =>
            {
                var tiles = _spells.FreeTilesAround(location, 3f, true);
                var count = _random.Next(0, 4);
                for (var i = 0; i < count && tiles.Count > 0; i++)
                {
                    Spawn(_random.Pick(RainFood), _random.PickAndTake(tiles));
                }
            });
        }
    }

    /// <summary>spawn_delayed_mobs: порталы, из которых через 10–15 секунд выходят существа.</summary>
    private void DelayedMobs(int potency, EntityCoordinates location)
    {
        var count = (int) Lerp(1, 4, potency);
        var tiles = _spells.FreeTilesAround(location, 3f, true);
        var mob = _random.Pick(PermittedMobs);
        for (var i = 0; i < count && tiles.Count > 0; i++)
        {
            var spot = _random.PickAndTake(tiles);
            var delay = TimeSpan.FromSeconds(_random.NextFloat(10f, 15f));
            var portal = Spawn("ImperialDelayedMobPortal", spot);
            EnsureComp<TimedDespawnComponent>(portal).Lifetime = (float) delay.TotalSeconds;
            Schedule(delay, () =>
            {
                _audio.PlayPvs(new SoundPathSpecifier("/Audio/Imperial/Wizard/teleport_app.ogg"), spot);
                Spawn("EffectSparks", spot);
                Spawn(mob, spot);
            });
        }
    }

    /// <summary>orchestra: обезьяны-официанты с инструментами.</summary>
    private void Orchestra(int potency, EntityCoordinates location)
    {
        var count = (int) Lerp(1, 4, potency);
        var tiles = _spells.FreeTilesAround(location, 2f, true);
        for (var i = 0; i < count && tiles.Count > 0; i++)
        {
            var monkey = Spawn("MobMonkey", _random.PickAndTake(tiles));
            var instrument = Spawn(_random.Pick(Instruments), Transform(monkey).Coordinates);
            _hands.TryPickupAnyHand(monkey, instrument);
        }
    }
}
