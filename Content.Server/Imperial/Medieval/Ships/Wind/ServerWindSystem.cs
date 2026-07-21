using System;
using Content.Server.Shuttles.Components;
using Content.Server.Weather;
using Content.Shared.Imperial.Medieval.Administration.Ships;
using Content.Shared.Imperial.Medieval.Ships.Sea;
using Content.Shared.Parallax;
using Content.Shared.Weather;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Medieval.Ships.Wind;

/// <summary>
/// This handles...
/// </summary>
public sealed class ServerWindSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly WeatherSystem _weather = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private TimeSpan _nextCheckTime;
    private TimeSpan _nextStormCheckTime;

    public override void Initialize()
    {
        SubscribeLocalEvent<SeaComponent, ComponentInit>(OnSeaInit);
        SubscribeLocalEvent<StormLevelChangedEvent>(OnStormLevelChanged);

        _cfg.OnValueChanged(ShipsCCVars.StormLevel, OnStormLevelCVarChanged, true);
    }

    private void OnSeaInit(EntityUid uid, SeaComponent component, ComponentInit args)
    {
        _cfg.SetCVar(ShipsCCVars.StormLevel, component.InitStormLevel);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;

        if (curTime > _nextCheckTime)
        {
            _nextCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.WindChangeTime));
            RandomiseVind();
        }

        if (curTime > _nextStormCheckTime)
        {
            _nextStormCheckTime = curTime + TimeSpan.FromSeconds(_cfg.GetCVar(ShipsCCVars.StormChangeTime));
            RandomiseStormLevel();
        }
    }

    private void RandomiseVind()
    {
        var windForce = _cfg.GetCVar(ShipsCCVars.StormLevel);
        var countShips = FindShips();

        if (windForce <= 0 + countShips)
            windForce += _random.Next(0, 2);
        else if (windForce >= 2 + countShips || countShips >= 10)
            windForce -= _random.Next(0, 2);
        else
            windForce += _random.Next(-1, 2);
        _cfg.SetCVar(ShipsCCVars.WindPower, windForce);

        var windAngle = _cfg.GetCVar(ShipsCCVars.WindRotation);
        windAngle += _random.Next(-1, 1) * 5;

        if (Math.Abs(windAngle) > 360)
            windAngle -= 360;
        else if (windAngle < 0)
            windAngle += 360;

        _cfg.SetCVar(ShipsCCVars.WindRotation, windAngle);
    }

    private void RandomiseStormLevel()
    {
        var stormLevel = _cfg.GetCVar(ShipsCCVars.StormLevel);
        var increaseChance = _cfg.GetCVar(ShipsCCVars.StormIncreaseChance);
        var decreaseChance = _cfg.GetCVar(ShipsCCVars.StormDecreaseChance);
        var minStormLevel = _cfg.GetCVar(ShipsCCVars.StormMinLevel);
        var maxStormLevel = _cfg.GetCVar(ShipsCCVars.StormMaxLevel);
        var roll = _random.NextFloat();

        if (roll < increaseChance)
            stormLevel += 1f;
        else if (roll < increaseChance + decreaseChance)
            stormLevel -= 1f;

        stormLevel = Math.Clamp(stormLevel, minStormLevel, maxStormLevel);
        if (Math.Abs(stormLevel - _cfg.GetCVar(ShipsCCVars.StormLevel)) > 0.001f)
            _cfg.SetCVar(ShipsCCVars.StormLevel, stormLevel);
    }

    private int FindShips()
    {
        var count = 0;
        foreach (var seaComp in EntityManager.EntityQuery<SeaComponent>())
        {
            if (seaComp.Disabled)
                continue;

            var mapId = _transform.GetMapId(seaComp.Owner);
            var ships = new HashSet<Entity<ShuttleComponent>>();
            _lookup.GetEntitiesOnMap(mapId, ships);
            count += ships.Count;
        }

        return count;
    }

    private void OnStormLevelCVarChanged(float stormLevel)
    {
        var minStormLevel = _cfg.GetCVar(ShipsCCVars.StormMinLevel);
        var maxStormLevel = _cfg.GetCVar(ShipsCCVars.StormMaxLevel);
        var clampedLevel = Math.Clamp(stormLevel, minStormLevel, maxStormLevel);
        if (Math.Abs(stormLevel - clampedLevel) > 0.001f)
        {
            _cfg.SetCVar(ShipsCCVars.StormLevel, clampedLevel);
            return;
        }

        RaiseLocalEvent(new StormLevelChangedEvent(clampedLevel));
    }

    private void OnStormLevelChanged(StormLevelChangedEvent args)
    {
        ApplyStormLevel(args.StormLevel);
    }

    private void ApplyStormLevel(float stormLevel)
    {
        UpdateStormWindPower(stormLevel);
        UpdateStormWeather(stormLevel);
        UpdateStormParallax(stormLevel);
    }

    private void UpdateStormWindPower(float stormLevel)
    {
        _cfg.SetCVar(ShipsCCVars.WindPower, stormLevel);
    }

    private void UpdateStormWeather(float stormLevel)
    {
        var rainLevelReached = stormLevel >= _cfg.GetCVar(ShipsCCVars.StormRainLevel);
        var rainWeatherId = new ProtoId<WeatherPrototype>(_cfg.GetCVar(ShipsCCVars.StormRainWeather));
        WeatherPrototype? rain = null;
        if (rainLevelReached && !_prototype.TryIndex(rainWeatherId, out rain))
            return;

        var stormLevelReached = stormLevel >= _cfg.GetCVar(ShipsCCVars.StormStormLevel);
        var stormWeatherId = new ProtoId<WeatherPrototype>(_cfg.GetCVar(ShipsCCVars.StormStormWeather));
        WeatherPrototype? storm = null;
        if (stormLevelReached && !_prototype.TryIndex(stormWeatherId, out storm))
            return;

        var seaMaps = new HashSet<MapId>();
        foreach (var seaComp in EntityManager.EntityQuery<SeaComponent>())
        {
            if (seaComp.Disabled)
                continue;

            var mapId = _transform.GetMapId(seaComp.Owner);
            if (mapId == MapId.Nullspace || !seaMaps.Add(mapId))
                continue;

            if (rainLevelReached)
                _weather.SetWeather(mapId, rain!, null);
            else
                DisableWeather(mapId, rainWeatherId);

            if (stormLevelReached)
                _weather.SetWeather(mapId, storm!, null);
            else
                DisableWeather(mapId, stormWeatherId);
        }
    }

    private void DisableWeather(MapId mapId, ProtoId<WeatherPrototype> weatherId)
    {
        if (!_mapSystem.TryGetMap(mapId, out var mapUid) ||
            !TryComp<WeatherComponent>(mapUid.Value, out var weatherComp))
        {
            return;
        }

        if (!weatherComp.Weather.TryGetValue(weatherId, out var weatherData))
            return;

        var endTime = _timing.CurTime + WeatherComponent.ShutdownTime;
        if (weatherData.EndTime != null && weatherData.EndTime <= endTime)
            return;

        weatherData.EndTime = endTime;
        Dirty(mapUid.Value, weatherComp);
    }

    private void UpdateStormParallax(float stormLevel)
    {
        var seaMaps = new HashSet<MapId>();
        foreach (var seaComp in EntityManager.EntityQuery<SeaComponent>())
        {
            if (seaComp.Disabled)
                continue;

            var seaParallax = GetStormParallax(stormLevel, seaComp);

            var mapId = _transform.GetMapId(seaComp.Owner);
            if (mapId == MapId.Nullspace || !seaMaps.Add(mapId))
                continue;

            if (!_mapSystem.TryGetMap(mapId, out var mapUid))
                continue;

            var parallax = EnsureComp<ParallaxComponent>(mapUid.Value);
            if (parallax.Parallax == seaParallax)
                continue;

            parallax.Parallax = seaParallax;
            Dirty(mapUid.Value, parallax);
        }
    }

    private static string GetStormParallax(float stormLevel, SeaComponent sea)
    {
        if (stormLevel >= 7f)
            return sea.StormParallax3;
        if (stormLevel >= 5f)
            return sea.StormParallax2;
        if (stormLevel >= 3f)
            return sea.StormParallax1;

        return sea.CalmParallax;
    }
}

public sealed class StormLevelChangedEvent : EntityEventArgs
{
    public readonly float StormLevel;

    public StormLevelChangedEvent(float stormLevel)
    {
        StormLevel = stormLevel;
    }
}
