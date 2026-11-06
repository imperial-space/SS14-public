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
using Robust.Shared.Timing;
using Content.Shared.FixedPoint;

namespace Content.Server.Imperial.Lavaland.Storm;

public sealed class LavalandStormSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;

    private static readonly DamageSpecifier StormDamage = new() { DamageDict = { ["Blunt"] = 2, ["Heat"] = 1 } };

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LavalandMapComponent, ComponentStartup>(OnStartup);
    }

    private void OnStartup(EntityUid uid, LavalandMapComponent comp, ComponentStartup args)
    {
        comp.NextStorm = _timing.CurTime + RandomInterval(comp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<LavalandMapComponent>();
        while (query.MoveNext(out var mapUid, out var comp))
            UpdateMap(mapUid, comp);
    }

    private TimeSpan RandomInterval(LavalandMapComponent comp)
    {
        return comp.MinStormInterval + (comp.MaxStormInterval - comp.MinStormInterval) * _random.NextDouble();
    }

    private void UpdateMap(EntityUid mapUid, LavalandMapComponent comp)
    {
        var now = _timing.CurTime;
        switch (comp.StormState)
        {
            case LavalandStormState.Idle:
                if (now >= comp.NextStorm)
                    DecideStormEvent(mapUid, comp);
                break;

            case LavalandStormState.Warning:
                if (now >= comp.StateEnd)
                    ActivateStorm(mapUid, comp);
                break;

            case LavalandStormState.PassingBy:
                if (now >= comp.StateEnd)
                {
                    BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-passed-by"));
                    ResetStorm(mapUid, comp);
                }
                break;

            case LavalandStormState.Active:
                if (now >= comp.NextDamage)
                {
                    comp.NextDamage = now + comp.DamageInterval;
                    ApplyStormDamage(mapUid);
                }

                if (now >= comp.StateEnd)
                    EndStorm(mapUid, comp);
                break;

            case LavalandStormState.Ending:
                if (now >= comp.StateEnd)
                {
                    _weather.TryRemoveWeather(mapUid, comp.LightWeather);
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
        _weather.TryAddWeather(mapUid, comp.LightWeather, out _);

        if (roll < comp.NoEventChance + comp.PassByChance)
        {
            comp.StormState = LavalandStormState.PassingBy;
            comp.StateEnd = _timing.CurTime + comp.WarningDuration;
        }
        else
        {
            comp.StormState = LavalandStormState.Warning;
            comp.StateEnd = _timing.CurTime + comp.WarningDuration;
        }
    }

    /// <summary>
    /// weather.wind_down: завершить надвигающуюся или идущую бурю (посох бурь).
    /// Возвращает false, если бури нет или она уже стихает.
    /// </summary>
    public bool TryWindDown(EntityUid mapUid, LavalandMapComponent comp)
    {
        switch (comp.StormState)
        {
            case LavalandStormState.Warning:
            case LavalandStormState.PassingBy:
            case LavalandStormState.Active:
                EndStorm(mapUid, comp);
                return true;
            default:
                return false;
        }
    }

    public void ForceStartStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        ActivateStorm(mapUid, comp);
    }

    private void ActivateStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        comp.StormState = LavalandStormState.Active;
        comp.StateEnd = _timing.CurTime + comp.StormDuration;
        comp.NextDamage = _timing.CurTime;
        BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-started"));
        _weather.TryRemoveWeather(mapUid, comp.LightWeather);
        _weather.TryAddWeather(mapUid, comp.HeavyWeather, out _);
    }

    private void EndStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        comp.StormState = LavalandStormState.Ending;
        comp.StateEnd = _timing.CurTime + comp.StormEndingDuration;
        BroadcastToMap(mapUid, Loc.GetString("lavaland-storm-ended"));
        _weather.TryRemoveWeather(mapUid, comp.HeavyWeather);
        _weather.TryAddWeather(mapUid, comp.LightWeather, out _);
    }

    private void ResetStorm(EntityUid mapUid, LavalandMapComponent comp)
    {
        comp.StormState = LavalandStormState.Idle;
        comp.NextStorm = _timing.CurTime + RandomInterval(comp);
        _weather.TryRemoveWeather(mapUid, comp.LightWeather);
        _weather.TryRemoveWeather(mapUid, comp.HeavyWeather);
    }

    private void ApplyStormDamage(EntityUid mapUid)
    {
        var damage = new DamageSpecifier(StormDamage);

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
