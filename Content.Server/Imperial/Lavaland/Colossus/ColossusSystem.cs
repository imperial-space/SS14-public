using Content.Server.Chat.Systems;
using Content.Server.Imperial.Lavaland.MegafaunaSleep;
using Content.Server.Popups;
using Content.Shared.Chat;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Lavaland.Colossus;

public sealed class ColossusSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movement = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xformSys = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ColossusComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ColossusComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var damageable, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (HasComp<LavalandMegafaunaSleepComponent>(uid))
                continue;

            var totalDamage = _damageable.GetPositiveDamage((uid, damageable)).GetTotal().Float();
            var healthRatio = totalDamage / comp.MaxHp;

            // Spiral blocks all other attacks
            if (comp.IsSpiralActive)
            {
                ProcessSpiral(uid, comp, healthRatio);
                continue;
            }

            // Start spiral when cooldown passed (available at any HP; double spiral below 50%)
            if (_timing.CurTime >= comp.NextSpiralTime)
            {
                StartSpiral(uid, comp, healthRatio);
                continue;
            }

            // Process ongoing cross/diagonal sequence
            if (comp.CrossIsFiring)
                ProcessCrossSequence(uid, comp);

            // Start new cross sequence if not already firing and cooldown passed
            if (!comp.CrossIsFiring && _timing.CurTime >= comp.NextCrossTime)
                StartCrossSequence(uid, comp);

            // Attack 1: Cone - requires a visible target
            if (_timing.CurTime >= comp.NextConeTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var coneTarget))
            {
                DoConeAttack(uid, coneTarget, comp);
            }

            // Attack 3: Random scatter
            if (_timing.CurTime >= comp.NextRandomTime)
                DoRandomAttack(uid, comp);
        }
    }

    // ── Enrage ───────────────────────────────────────────────────────────────

    private void OnDamageChanged(EntityUid uid, ColossusComponent comp, DamageChangedEvent args)
    {
        if (comp.Enraged)
            return;
        if (TryComp<MobStateComponent>(uid, out var ms) && ms.CurrentState != MobState.Alive)
            return;
        if (!args.DamageIncreased)
            return;

        var total = _damageable.GetPositiveDamage((uid, args.Damageable)).GetTotal().Float();
        if (total < comp.EnrageThreshold)
            return;

        SetEnraged(uid, comp, true);
    }

    private void SetEnraged(EntityUid uid, ColossusComponent comp, bool enraged)
    {
        if (comp.Enraged == enraged)
            return;

        comp.Enraged = enraged;
        _appearance.SetData(uid, ColossusVisuals.Enraged, enraged);
        _audio.PlayPvs(comp.EnrageSound, uid);

        var speed = enraged ? comp.EnragedSpeed : comp.NormalSpeed;
        _movement.ChangeBaseSpeed(uid, speed, speed, 20f);

        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        var dmg = enraged ? comp.EnragedMeleeDamage : comp.NormalMeleeDamage;
        var spec = new DamageSpecifier();
        spec.DamageDict.Add("Blunt", FixedPoint2.New(dmg));
        melee.Damage = spec;
        Dirty(uid, melee);
    }

    // ── Attack 1: Cone ───────────────────────────────────────────────────────

    private void DoConeAttack(EntityUid uid, EntityUid target, ColossusComponent comp)
    {
        _audio.PlayPvs(comp.AttackSound, uid);

        var origin = Transform(uid).Coordinates;

        // Use world positions so coordinates are in the same space
        var bossWorld = _xformSys.GetWorldPosition(uid);
        var targetWorld = _xformSys.GetWorldPosition(target);
        var delta = targetWorld - bossWorld;
        var baseAngle = MathF.Atan2(delta.Y, delta.X);
        var halfSpread = comp.ConeSpreadDeg * (MathF.PI / 180f) / 2f;

        for (var i = 0; i < comp.ConeCount; i++)
        {
            var t = comp.ConeCount <= 1 ? 0f : (float)i / (comp.ConeCount - 1) - 0.5f;
            var angle = baseAngle + t * 2f * halfSpread;
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            SpawnSpike(uid, origin, dir, comp);
        }

        comp.NextConeTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ConeCooldown);
    }

    // ── Attack 2: Cross/Diagonal sequence ────────────────────────────────────

    private void StartCrossSequence(EntityUid uid, ColossusComponent comp)
    {
        comp.CrossIsFiring = true;
        comp.CrossRepeatsDone = 0;
        comp.NextCrossRepeatTime = _timing.CurTime; // fire first volley immediately
    }

    private void ProcessCrossSequence(EntityUid uid, ColossusComponent comp)
    {
        if (_timing.CurTime < comp.NextCrossRepeatTime)
            return;

        if (comp.CrossRepeatsDone >= comp.CrossRepeatTotal)
        {
            comp.CrossIsFiring = false;
            comp.NextCrossTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CrossCooldown);
            return;
        }

        FireCrossVolley(uid, comp);
        comp.CrossRepeatsDone++;
        comp.CrossNextCardinal = !comp.CrossNextCardinal;
        comp.NextCrossRepeatTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CrossRepeatDelay);
    }

    private void FireCrossVolley(EntityUid uid, ColossusComponent comp)
    {
        _audio.PlayPvs(comp.AttackSound, uid);
        var origin = Transform(uid).Coordinates;

        float[] angles = comp.CrossNextCardinal
            ? new[] { 0f, 90f, 180f, 270f }   // cardinal
            : new[] { 45f, 135f, 225f, 315f };  // diagonal

        foreach (var deg in angles)
        {
            var rad = deg * (MathF.PI / 180f);
            SpawnSpike(uid, origin, new Vector2(MathF.Cos(rad), MathF.Sin(rad)), comp);
        }
    }

    // ── Attack 3: Random scatter ──────────────────────────────────────────────

    private void DoRandomAttack(EntityUid uid, ColossusComponent comp)
    {
        _audio.PlayPvs(comp.AttackSound, uid);
        var origin = Transform(uid).Coordinates;
        var half = comp.RandomAreaHalfSize;

        for (var x = -half; x <= half; x++)
        {
            for (var y = -half; y <= half; y++)
            {
                if (!_random.Prob(comp.RandomChance))
                    continue;

                var offset = new Vector2(x, y);
                if (offset.Length() < 0.5f)
                    continue; // skip self-tile

                SpawnSpike(uid, origin, Vector2.Normalize(offset), comp);
            }
        }

        comp.NextRandomTime = _timing.CurTime + TimeSpan.FromSeconds(comp.RandomCooldown);
    }

    // ── Attack 4: Spiral ──────────────────────────────────────────────────────

    private void StartSpiral(EntityUid uid, ColossusComponent comp, float healthRatio)
    {
        comp.IsSpiralActive = true;
        comp.SpiralSpikeFired = 0;
        comp.SpiralCurrentAngleDeg = 0f;
        comp.NextSpiralSpikeTime = _timing.CurTime;

        // healthRatio >= 0.5 means damage >= 50% maxHp => HP <= 50%
        var word = healthRatio >= 0.5f ? "DIE" : "JUDGMENT";

        // Boss speaks the word in IC chat (audible to all nearby)
        _chat.TrySendInGameICMessage(uid, word, InGameICChatType.Speak, false, hideLog: true);
        _audio.PlayPvs(comp.EnrageSound, uid);

        // Send a personal large red popup to every nearby alive player
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } player)
                continue;

            if (!Exists(player))
                continue;

            if (!TryComp<MobStateComponent>(player, out var ms) || ms.CurrentState != MobState.Alive)
                continue;

            if (!Transform(uid).Coordinates.TryDistance(EntityManager,
                    Transform(player).Coordinates, out var dist) || dist > comp.TargetSearchRange)
                continue;

            _popup.PopupEntity(word, uid, player, PopupType.LargeCaution);
        }
    }

    private void ProcessSpiral(EntityUid uid, ColossusComponent comp, float healthRatio)
    {
        if (_timing.CurTime < comp.NextSpiralSpikeTime)
            return;

        if (comp.SpiralSpikeFired >= comp.SpiralSpikeCount)
        {
            comp.IsSpiralActive = false;
            comp.NextSpiralTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SpiralCooldown);
            return;
        }

        var origin = Transform(uid).Coordinates;
        var cwRad = comp.SpiralCurrentAngleDeg * (MathF.PI / 180f);
        SpawnSpike(uid, origin, new Vector2(MathF.Cos(cwRad), MathF.Sin(cwRad)), comp);

        // Below 50% HP: second arm goes counter-clockwise
        if (healthRatio >= 0.5f)
        {
            var ccwRad = -comp.SpiralCurrentAngleDeg * (MathF.PI / 180f);
            SpawnSpike(uid, origin, new Vector2(MathF.Cos(ccwRad), MathF.Sin(ccwRad)), comp);
        }

        comp.SpiralCurrentAngleDeg += comp.SpiralAngleStepDeg;
        comp.SpiralSpikeFired++;
        comp.NextSpiralSpikeTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SpiralSpikeInterval);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SpawnSpike(EntityUid bossUid, EntityCoordinates origin, Vector2 direction, ColossusComponent comp)
    {
        var spike = Spawn(comp.SpikePrototype, origin);

        // Face direction of travel
        _xformSys.SetWorldRotation(spike, new Robust.Shared.Maths.Angle(Math.Atan2(direction.Y, direction.X)));

        // Prevent boss from taking damage from own projectiles
        if (TryComp<ProjectileComponent>(spike, out var projComp))
        {
            projComp.Shooter = bossUid;
            Dirty(spike, projComp);
        }

        // Apply velocity
        _physics.SetLinearVelocity(spike, direction * comp.SpikeSpeed);
    }

    private bool TryFindNearbyPlayer(EntityUid uid, float range, out EntityUid result)
    {
        result = EntityUid.Invalid;
        var myPos = Transform(uid).Coordinates;
        var best = float.MaxValue;

        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } candidate)
                continue;

            if (!Exists(candidate))
                continue;

            if (!TryComp<MobStateComponent>(candidate, out var ms) || ms.CurrentState != MobState.Alive)
                continue;

            if (!myPos.TryDistance(EntityManager, Transform(candidate).Coordinates, out var dist))
                continue;

            if (dist > range || dist >= best)
                continue;

            best = dist;
            result = candidate;
        }

        return result.Valid;
    }
}
