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

            // Spiral blocks everything else
            if (comp.IsSpiralActive)
            {
                ProcessSpiral(uid, comp, healthRatio);
                continue;
            }

            // Telegraph: wait 1.5s before firing (SS13 SLEEP_CHECK_DEATH pattern)
            if (comp.IsTelegraphing)
            {
                if (_timing.CurTime >= comp.TelegraphUntil)
                    ExecutePendingAttack(uid, comp, healthRatio);
                continue;
            }

            // Cross sequence runs to completion without interruption
            if (comp.CrossIsFiring)
            {
                ProcessCrossSequence(uid, comp);
                continue;
            }

            // Spiral: announce → telegraph 1.5s → start
            if (_timing.CurTime >= comp.NextSpiralTime)
            {
                AnnounceSpiralAttack(uid, comp);
                continue;
            }

            // Cross: announce → telegraph 1.5s → start sequence
            if (_timing.CurTime >= comp.NextCrossTime)
            {
                AnnounceCrossAttack(uid, comp);
                continue;
            }

            // Random AoE: announce → telegraph 1.5s → fire
            if (_timing.CurTime >= comp.NextRandomTime)
            {
                AnnounceRandomAttack(uid, comp);
                continue;
            }

            // Shotgun cone: announce → telegraph 1.5s → fire (requires nearby player)
            if (_timing.CurTime >= comp.NextConeTime &&
                TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var coneTarget))
            {
                AnnounceConeAttack(uid, coneTarget, comp);
            }
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

    // ── Telegraph helpers ─────────────────────────────────────────────────────

    private void BeginTelegraph(EntityUid uid, ColossusComponent comp, ColossusPreFireAttack attack, EntityUid coneTarget = default)
    {
        comp.IsTelegraphing = true;
        comp.TelegraphUntil = _timing.CurTime + TimeSpan.FromSeconds(comp.TelegraphDelay);
        comp.PendingAttack = attack;
        comp.TelegraphConeTarget = coneTarget;
    }

    private void ExecutePendingAttack(EntityUid uid, ColossusComponent comp, float healthRatio)
    {
        comp.IsTelegraphing = false;
        var attack = comp.PendingAttack;
        comp.PendingAttack = ColossusPreFireAttack.None;

        switch (attack)
        {
            case ColossusPreFireAttack.Cone:
                if (Exists(comp.TelegraphConeTarget))
                    FireConeNow(uid, comp.TelegraphConeTarget, comp);
                break;
            case ColossusPreFireAttack.Cross:
                comp.CrossIsFiring = true;
                comp.CrossRepeatsDone = 0;
                comp.NextCrossRepeatTime = _timing.CurTime;
                break;
            case ColossusPreFireAttack.Random:
                FireRandomNow(uid, comp);
                break;
            case ColossusPreFireAttack.Spiral:
                comp.IsSpiralActive = true;
                comp.SpiralSpikeFired = 0;
                comp.SpiralCurrentAngleDeg = 0f;
                comp.NextSpiralSpikeTime = _timing.CurTime;
                SendSpiralPopups(uid, comp);
                break;
        }
    }

    // ── Attack 1: Shotgun cone ────────────────────────────────────────────────

    private void AnnounceConeAttack(EntityUid uid, EntityUid target, ColossusComponent comp)
    {
        _chat.TrySendInGameICMessage(uid, "Retribution.", InGameICChatType.Speak, false, hideLog: true);
        _audio.PlayPvs(comp.AttackSound, uid);
        comp.NextConeTime = _timing.CurTime + TimeSpan.FromSeconds(comp.ConeCooldown);
        BeginTelegraph(uid, comp, ColossusPreFireAttack.Cone, target);
    }

    private void FireConeNow(EntityUid uid, EntityUid target, ColossusComponent comp)
    {
        var origin = Transform(uid).Coordinates;
        var bossWorld = _xformSys.GetWorldPosition(uid);
        var targetWorld = _xformSys.GetWorldPosition(target);
        var delta = targetWorld - bossWorld;
        var baseAngle = MathF.Atan2(delta.Y, delta.X);
        var halfSpread = comp.ConeSpreadDeg * (MathF.PI / 180f) / 2f;

        for (var i = 0; i < comp.ConeCount; i++)
        {
            var t = comp.ConeCount <= 1 ? 0f : (float)i / (comp.ConeCount - 1) - 0.5f;
            var angle = baseAngle + t * 2f * halfSpread;
            SpawnSpike(uid, origin, new Vector2(MathF.Cos(angle), MathF.Sin(angle)), comp);
        }
    }

    // ── Attack 2: Directional alternating ────────────────────────────────────

    private void AnnounceCrossAttack(EntityUid uid, ColossusComponent comp)
    {
        _chat.TrySendInGameICMessage(uid, "Lament.", InGameICChatType.Speak, false, hideLog: true);
        _audio.PlayPvs(comp.AttackSound, uid);
        comp.NextCrossTime = _timing.CurTime + TimeSpan.FromSeconds(comp.CrossCooldown);
        BeginTelegraph(uid, comp, ColossusPreFireAttack.Cross);
    }

    private void ProcessCrossSequence(EntityUid uid, ColossusComponent comp)
    {
        if (_timing.CurTime < comp.NextCrossRepeatTime)
            return;

        if (comp.CrossRepeatsDone >= comp.CrossRepeatTotal)
        {
            comp.CrossIsFiring = false;
            comp.CrossNextCardinal = false; // reset to diagonals-first for next sequence
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

    // ── Attack 3: Random AoE ─────────────────────────────────────────────────

    private void AnnounceRandomAttack(EntityUid uid, ColossusComponent comp)
    {
        _chat.TrySendInGameICMessage(uid, "Wrath.", InGameICChatType.Speak, false, hideLog: true);
        _audio.PlayPvs(comp.AttackSound, uid);
        comp.NextRandomTime = _timing.CurTime + TimeSpan.FromSeconds(comp.RandomCooldown);
        BeginTelegraph(uid, comp, ColossusPreFireAttack.Random);
    }

    private void FireRandomNow(EntityUid uid, ColossusComponent comp)
    {
        var origin = Transform(uid).Coordinates;
        for (var i = 0; i < comp.RandomShotCount; i++)
        {
            var angleDeg = _random.NextFloat(0f, 360f);
            var rad = angleDeg * (MathF.PI / 180f);
            SpawnSpike(uid, origin, new Vector2(MathF.Cos(rad), MathF.Sin(rad)), comp);
        }
    }

    // ── Attack 4: Spiral ──────────────────────────────────────────────────────

    private void AnnounceSpiralAttack(EntityUid uid, ColossusComponent comp)
    {
        _chat.TrySendInGameICMessage(uid, "Judgement.", InGameICChatType.Speak, false, hideLog: true);
        _audio.PlayPvs(comp.EnrageSound, uid);
        comp.NextSpiralTime = _timing.CurTime + TimeSpan.FromSeconds(comp.SpiralCooldown);
        BeginTelegraph(uid, comp, ColossusPreFireAttack.Spiral);
    }

    private void SendSpiralPopups(EntityUid uid, ColossusComponent comp)
    {
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

            _popup.PopupEntity("Judgement.", uid, player, PopupType.LargeCaution);
        }
    }

    private void ProcessSpiral(EntityUid uid, ColossusComponent comp, float healthRatio)
    {
        if (_timing.CurTime < comp.NextSpiralSpikeTime)
            return;

        if (comp.SpiralSpikeFired >= comp.SpiralSpikeCount)
        {
            comp.IsSpiralActive = false;
            return;
        }

        var origin = Transform(uid).Coordinates;
        var cwRad = comp.SpiralCurrentAngleDeg * (MathF.PI / 180f);
        SpawnSpike(uid, origin, new Vector2(MathF.Cos(cwRad), MathF.Sin(cwRad)), comp);

        // Below 1/3 HP: second arm counter-clockwise (matches SS13: health <= maxHp/3)
        if (healthRatio >= 2f / 3f)
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

        _xformSys.SetWorldRotation(spike, new Robust.Shared.Maths.Angle(Math.Atan2(direction.Y, direction.X)));

        if (TryComp<ProjectileComponent>(spike, out var projComp))
        {
            projComp.Shooter = bossUid;
            Dirty(spike, projComp);
        }

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
