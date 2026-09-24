using Content.Server.Atmos.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Timer = Robust.Shared.Timing.Timer;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticVoidConduitSystem : EntitySystem
{
    [Dependency] private readonly DamageableSystem           _damage         = default!;
    [Dependency] private readonly EntityLookupSystem         _lookup         = default!;
    [Dependency] private readonly SharedAudioSystem          _audio          = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly IRobustRandom              _random         = default!;
    [Dependency] private readonly IGameTiming                _timing         = default!;
    [Dependency] private readonly SharedTransformSystem      _xform          = default!;

    private static readonly SoundPathSpecifier AmbientSound =
        new("/Audio/Imperial/heretic/sound_ambience_misc_ambiatm1.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticVoidConduitComponent, MapInitEvent>(OnConduitMapInit);
        SubscribeLocalEvent<HereticVoidConduitComponent, ComponentRemove>(OnConduitRemove);
    }

    private void OnConduitMapInit(EntityUid uid, HereticVoidConduitComponent comp, MapInitEvent args)
    {
        comp.AmbientSound = _audio.PlayPvs(AmbientSound, uid, AudioParams.Default.WithLoop(true))?.Entity;
        SpawnTileOverlays(uid, comp);
        SpawnVisualEffects(uid, comp);
    }

    private void OnConduitRemove(EntityUid uid, HereticVoidConduitComponent comp, ComponentRemove args)
    {
        if (comp.AmbientSound.HasValue)
            _audio.Stop(comp.AmbientSound.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticVoidConduitComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            comp.LifetimeTimer += frameTime;
            if (comp.LifetimeTimer >= comp.Lifetime)
            {
                QueueDel(uid);
                continue;
            }

            comp.PulseTimer += frameTime;
            if (comp.PulseTimer < comp.PulseInterval)
                continue;

            comp.PulseTimer = 0f;
            StartPulseWave(uid, comp);
        }

        var now = _timing.CurTime;
        var pressureQuery = EntityQueryEnumerator<VoidConduitPressureComponent>();
        while (pressureQuery.MoveNext(out var target, out var pressure))
        {
            if (now < pressure.ExpiresAt)
                continue;

            RemComp<VoidConduitPressureComponent>(target);
            RemComp<PressureImmunityComponent>(target);
        }
    }

    private static readonly string[] WaveRings =
    [
        "HereticVoidConduitWaveRing1",
        "HereticVoidConduitWaveRing3",
        "HereticVoidConduitWaveRing6",
        "HereticVoidConduitWaveRing9",
        "HereticVoidConduitWaveRing12",
    ];

    private void SpawnVisualEffects(EntityUid uid, HereticVoidConduitComponent comp)
    {
        var coords = Transform(uid).Coordinates;
        // Кольца появляются с интервалом 2.5 с, имитируя плавный рост волны как в SS13 (0→12 за 12 с)
        for (var i = 0; i < WaveRings.Length; i++)
        {
            var capturedProto = WaveRings[i];
            var capturedCoords = coords;
            Timer.Spawn(TimeSpan.FromSeconds(i * 2.5), () =>
            {
                if (!Exists(uid)) return;
                Spawn(capturedProto, capturedCoords);
            });
        }
    }

    private void SpawnTileOverlays(EntityUid uid, HereticVoidConduitComponent comp)
    {
        var xform = Transform(uid);
        if (!xform.GridUid.HasValue)
            return;

        // Снаппинг в LOCAL grid space — единственный правильный способ привязки к тайлам
        var local = xform.LocalPosition;
        var snapped = new Vector2(MathF.Floor(local.X) + 0.5f, MathF.Floor(local.Y) + 0.5f);
        var gridUid = xform.GridUid.Value;

        var radius = (int)comp.WaveRadius;
        var r2 = radius * radius;

        for (var dx = -radius; dx <= radius; dx++)
        {
            for (var dy = -radius; dy <= radius; dy++)
            {
                if (dx * dx + dy * dy > r2) continue;
                Spawn("HereticVoidConduitTileOverlay",
                    new EntityCoordinates(gridUid, snapped + new Vector2(dx, dy)));
            }
        }
    }

    private void StartPulseWave(EntityUid uid, HereticVoidConduitComponent comp)
    {
        SpawnVisualEffects(uid, comp);

        var conduitPos = _xform.GetWorldPosition(uid);
        var nearby = _lookup.GetEntitiesInRange(Transform(uid).Coordinates, comp.WaveRadius);

        var byDistance = new Dictionary<int, List<EntityUid>>();
        foreach (var target in nearby)
        {
            if (target == uid) continue;

            var targetPos = _xform.GetWorldPosition(target);
            var dist = (int)(targetPos - conduitPos).Length();

            if (!byDistance.TryGetValue(dist, out var list))
            {
                list = new List<EntityUid>();
                byDistance[dist] = list;
            }
            list.Add(target);
        }

        foreach (var (dist, targets) in byDistance)
        {
            var capturedTargets = targets;
            var capturedUid = uid;
            Timer.Spawn(TimeSpan.FromSeconds(dist), () =>
            {
                if (!Exists(capturedUid)) return;
                foreach (var target in capturedTargets)
                {
                    if (!Exists(target)) continue;
                    HandleWaveEffects(target);
                }
            });
        }
    }

    private void HandleWaveEffects(EntityUid target)
    {
        if (HasComp<MobStateComponent>(target))
        {
            if (HasComp<HereticComponent>(target))
            {
                var pressure = EnsureComp<VoidConduitPressureComponent>(target);
                pressure.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(15);
                EnsureComp<PressureImmunityComponent>(target);
            }
            else
            {
                _hereticEffects.ApplyVoidChill(target, 1);
            }
        }
        else if (HasComp<DamageableComponent>(target))
        {
            var dmg = new DamageSpecifier();
            dmg.DamageDict["Structural"] = FixedPoint2.New(_random.Next(5, 11));
            _damage.TryChangeDamage(target, dmg, ignoreResistances: true);
        }
    }
}
