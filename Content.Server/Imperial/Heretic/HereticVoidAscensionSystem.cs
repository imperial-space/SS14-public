using System.Linq;
using Content.Server.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Temperature.Components;
using Content.Server.Body;
using Content.Server.Station.Systems;
using Content.Server.Temperature.Systems;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Components;
using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Content.Shared.Speech.Muting;
using Content.Shared.Station.Components;
using Content.Shared.StatusEffect;
using Content.Shared.Weather;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// SS13 Void Ascension on_life equivalent:
/// every 2 seconds, simultaneously applies effects to all in range 10.
/// </summary>
public sealed class HereticVoidAscensionSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedWeatherSystem _weather = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;
    [Dependency] private readonly TemperatureSystem _temperature = default!;
    [Dependency] private readonly VisualBodySystem _visualBody = default!;

    private static readonly SoundPathSpecifier VoidAmbientSound =
        new("/Audio/Imperial/heretic/sound_ambience_antag_heretic_ascend_void.ogg");

    private static readonly EntProtoId VoidStormProto = "WeatherVoidStorm";

    // SS13: heretic_eyes.color_cutoffs = list(30, 30, 30) → #1e1e1e void eyes
    private static readonly Color VoidEyeColor = Color.FromHex("#1e1e1e");

    private const float WaveRadius = 10f;
    private const float WaveInterval = 2f;

    // SS13 TEMPERATURE_DAMAGE_COEFFICIENT = 6.08
    private const float TempCoeff = 6.08f;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticVoidAscendedComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<HereticVoidAscendedComponent, ComponentRemove>(OnRemove);
    }

    private void OnInit(EntityUid uid, HereticVoidAscendedComponent comp, ComponentInit args)
    {
        comp.AmbientSoundEntity = _audio.PlayPvs(VoidAmbientSound, uid, AudioParams.Default.WithLoop(true))?.Entity;

        // SS13: storm spawns on all station levels (SSmapping.levels_by_trait(ZTRAIT_STATION))
        // Iterate all grids that are station members directly — more reliable than Grids hashset.
        var memberQuery = EntityQueryEnumerator<StationMemberComponent>();
        while (memberQuery.MoveNext(out var gridUid, out _))
        {
            var mapId = Transform(gridUid).MapID;
            if (mapId == MapId.Nullspace) continue;
            if (!_mapSystem.TryGetMap(mapId, out var mapUid)) continue;
            if (comp.StormMapUids.Contains(mapUid.Value)) continue;
            comp.StormMapUids.Add(mapUid.Value);
            _weather.TryAddWeather(mapUid.Value, VoidStormProto, out _);
        }

        // SS13: heretic_eyes.color_cutoffs = list(30, 30, 30) → void black eyes
        comp.OriginalEyeColor = GetEyeColor(uid);
        SetEyeColor(uid, VoidEyeColor);

        // The void heretic is the source of the cold — they must not absorb it from the atmosphere they create.
        if (TryComp<TemperatureComponent>(uid, out var tempComp))
        {
            comp.OriginalAtmosTransferEfficiency = tempComp.AtmosTemperatureTransferEfficiency;
            tempComp.AtmosTemperatureTransferEfficiency = 0f;
        }
    }

    private void OnRemove(EntityUid uid, HereticVoidAscendedComponent comp, ComponentRemove args)
    {
        if (comp.AmbientSoundEntity.HasValue)
            _audio.Stop(comp.AmbientSoundEntity.Value);

        foreach (var mapUid in comp.StormMapUids)
            _weather.TryRemoveWeather(mapUid, VoidStormProto);
        comp.StormMapUids.Clear();

        if (comp.OriginalEyeColor.HasValue)
            SetEyeColor(uid, comp.OriginalEyeColor.Value);

        if (comp.OriginalAtmosTransferEfficiency.HasValue && TryComp<TemperatureComponent>(uid, out var tempComp))
            tempComp.AtmosTemperatureTransferEfficiency = comp.OriginalAtmosTransferEfficiency.Value;
    }

    private Color? GetEyeColor(EntityUid uid)
    {
        if (!_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out _))
            return null;
        foreach (var profile in profiles.Values)
            return profile.EyeColor;
        return null;
    }

    private void SetEyeColor(EntityUid uid, Color color)
    {
        if (!_visualBody.TryGatherMarkingsData(uid, null, out var profiles, out _, out _))
            return;
        var updated = profiles.ToDictionary(p => p.Key, p => p.Value with { EyeColor = color });
        _visualBody.ApplyProfiles(uid, updated);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticVoidAscendedComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.WaveTimer += frameTime;
            if (comp.WaveTimer < WaveInterval) continue;
            comp.WaveTimer = 0f;
            TriggerWave(uid);
        }
    }

    private void TriggerWave(EntityUid uid)
    {
        if (Deleted(uid)) return;

        var xform = Transform(uid);
        var coords = xform.Coordinates;

        // SS13 on_life: apply effects to ALL in range simultaneously (no wave delay)
        foreach (var target in _lookup.GetEntitiesInRange(coords, WaveRadius))
        {
            if (target == uid) continue;
            ApplyEntityEffect(target);
        }

        // Cool tile atmosphere in radius (SS13: isturf → environment.temperature *= 0.9)
        CoolTilesInRange(uid, xform);
    }

    private void ApplyEntityEffect(EntityUid target)
    {
        if (HasComp<MobStateComponent>(target))
        {
            if (HasComp<HereticComponent>(target))
            {
                // SS13: apply_status_effect(/datum/status_effect/void_conduit) → pressure immunity 15s
                var pressure = EnsureComp<VoidConduitPressureComponent>(target);
                pressure.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(15);
                EnsureComp<PressureImmunityComponent>(target);
                return;
            }

            // adjust_silence_up_to(2 SECONDS, 20 SECONDS)
            _statusEffects.TryAddStatusEffect<MutedComponent>(target, "Muted", TimeSpan.FromSeconds(20), true);

            // apply_status_effect(/datum/status_effect/void_chill, 1)
            _hereticEffects.ApplyVoidChill(target, 1);

            // adjust_eye_blur(rand(0 SECONDS, 2 SECONDS))
            _statusEffects.TryAddStatusEffect<TemporaryBlindnessComponent>(
                target,
                TemporaryBlindnessSystem.BlindingStatusEffect,
                TimeSpan.FromSeconds(_random.NextFloat(0f, 2f)),
                true);

            // adjust_bodytemperature(-30 * TEMPERATURE_DAMAGE_COEFFICIENT)
            _temperature.ChangeHeat(target, -30f * TempCoeff);
        }
        else if (HasComp<DamageableComponent>(target))
        {
            var dmg = new DamageSpecifier();
            // istype(/obj/machinery/door) → take_damage(rand(60, 80))
            // istype(/obj/structure/window) or grille → take_damage(rand(20, 40))
            var amount = HasComp<DoorComponent>(target)
                ? _random.Next(60, 81)
                : _random.Next(20, 41);
            dmg.DamageDict["Structural"] = FixedPoint2.New(amount);
            _damage.TryChangeDamage(target, dmg, ignoreResistances: true);
        }
    }

    private void CoolTilesInRange(EntityUid uid, TransformComponent xform)
    {
        var gridUid = xform.GridUid;
        var mapUid = xform.MapUid;
        var herTile = _xform.GetGridTilePositionOrDefault((uid, xform));
        var radius = (int)WaveRadius;
        var r2 = radius * radius;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > r2) continue;
                var tile = herTile + new Vector2i(dx, dy);
                // SS13: environment.temperature *= 0.9
                var mix = _atmos.GetTileMixture(gridUid, mapUid, tile);
                if (mix != null && !mix.Immutable)
                    mix.Temperature *= 0.9f;
            }
        }
    }
}
