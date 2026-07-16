using Content.Server.Chat.Managers;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Inventory;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Parallax.Biomes;
using Content.Shared.Weather;
using Robust.Server.Player;
using Robust.Shared.Enums;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.Server.Imperial.Lavaland.Storm;

public sealed class LavalandStormSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMapComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, LavalandMapComponent comp, ComponentStartup args)
    {
        comp.StormTimer = _random.NextFloat(comp.MinStormInterval, comp.MaxStormInterval);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var mapUid, out var comp))
            UpdateMap(mapUid, comp, frameTime);
    }

    private void UpdateMap(EntityUid mapUid, LavalandMapComponent comp, float frameTime)
    {
        switch (comp.StormState)
        {
            case LavalandStormState.Idle:
                comp.StormTimer -= frameTime;
                if (comp.StormTimer <= 0f)
                    DecideStormEvent(mapUid, comp);
                break;

            case LavalandStormState.Warning:
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                    ActivateStorm(mapUid, comp);
                break;

            case LavalandStormState.PassingBy:
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-passed-by"));
                    ResetStorm(mapUid, comp);
                }
                break;

            case LavalandStormState.Active:
                comp.DamageTimer -= frameTime;
                if (comp.DamageTimer <= 0f)
                {
                    comp.DamageTimer = comp.DamageInterval;
                    ApplyStormDamage(mapUid);
                }

                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                    EndStorm(mapUid, comp);
                break;

            case LavalandStormState.Ending:
                comp.StateTimer -= frameTime;
                if (comp.StateTimer <= 0f)
                {
                    _weather.TryRemoveWeather(mapUid, "WeatherAshfallLight");
                    ResetStorm(mapUid, comp);
                }
                break;
        }
    }

    private void DecideStormEvent(EntityUid mapUid, LavalandMapComponent comp)
    {
        var roll = _random.NextFloat();

        if (roll < comp.NoEventChance)
        {
            ResetStorm(mapUid, comp);
            return;
        }

        BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-warning"));
        _weather.TryAddWeather(mapUid, "WeatherAshfallLight", out _);

        if (roll < comp.NoEventChance + comp.PassByChance)
        {
            comp.StormState = LavalandStormState.PassingBy;
            comp.StateTimer = comp.WarningDuration;
        }
        else
        {
            comp.StormState = LavalandStormState.Warning;
            comp.StateTimer = comp.WarningDuration;
        }
    }

    public void ForceStartStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        ActivateStorm(mapUid, comp);
    }

    private void ActivateStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        comp.StormState = LavalandStormState.Active;
        comp.StateTimer = comp.StormDuration;
        comp.DamageTimer = 0f;
        BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-started"));
        _weather.TryRemoveWeather(mapUid, "WeatherAshfallLight");
        _weather.TryAddWeather(mapUid, "WeatherAshfallHeavy", out _);
    }

    private void EndStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        comp.StormState = LavalandStormState.Ending;
        comp.StateTimer = comp.StormEndingDuration;
        BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-ended"));
        _weather.TryRemoveWeather(mapUid, "WeatherAshfallHeavy");
        _weather.TryAddWeather(mapUid, "WeatherAshfallLight", out _);
    }

    private void ResetStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        comp.StormState = LavalandStormState.Idle;
        comp.StormTimer = _random.NextFloat(comp.MinStormInterval, comp.MaxStormInterval);
        _weather.TryRemoveWeather(mapUid, "WeatherAshfallLight");
        _weather.TryRemoveWeather(mapUid, "WeatherAshfallHeavy");
    }

    private void ApplyStormDamage(EntityUid mapUid)
    {
        var damage = new DamageSpecifier();
        damage.DamageDict["Blunt"] = 2;
        damage.DamageDict["Burn"] = 1;

        var query = EntityQueryEnumerator<MobStateComponent, DamageableComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var mobState, out _, out var xform))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;
            if (xform.MapUid != mapUid)
                continue;
            if (xform.GridUid == null || !HasComp<BiomeComponent>(xform.GridUid.Value))
                continue;
            if (IsImmuneToStorm(uid))
                continue;

            _damageable.TryChangeDamage(uid, damage, true, origin: mapUid);
        }
    }

    private bool IsImmuneToStorm(EntityUid uid)
    {
        if (HasComp<LavalandStormImmuneComponent>(uid))
            return true;
        if (_inventory.TryGetSlotEntity(uid, "outerClothing", out var outer) &&
            HasComp<LavalandStormImmuneComponent>(outer))
            return true;
        return false;
    }

    private void BroadcastToMap(EntityUid mapUid, string message)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame
                || session.AttachedEntity is not { Valid: true } entity)
                continue;

            var xform = Transform(entity);

            if (xform.MapUid != mapUid)
                continue;

            _chatManager.DispatchServerMessage(session, message);
        }
    }
}
