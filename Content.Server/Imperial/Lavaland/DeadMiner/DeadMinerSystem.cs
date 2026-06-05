using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.Weapons.Melee;
using Robust.Server.Player;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Enums;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using System.Numerics;

namespace Content.Server.Imperial.Lavaland.DeadMiner;

public sealed class DeadMinerSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _xformSys = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeadMinerComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var comp, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (!TryFindNearbyPlayer(uid, comp.TargetSearchRange, out var target))
                continue;

            var bossWorld = _xformSys.GetWorldPosition(uid);
            var targetWorld = _xformSys.GetWorldPosition(target);
            var dist = Vector2.Distance(bossWorld, targetWorld);

            // ── 1. Переключение режима атаки ────────────────────────────────
            UpdateMode(uid, comp, dist);

            // ── 2. Кинетический выстрел (1–4 тайла, только в transformed) ──
            if (comp.IsTransformed
                && dist >= comp.KineticMinRange
                && dist <= comp.KineticMaxRange
                && _timing.CurTime >= comp.NextKineticTime)
            {
                FireKinetic(uid, comp, bossWorld, targetWorld);
            }

            // ── 3. Прыжок (> 4 тайла) ──────────────────────────────────────
            if (dist > comp.JumpTriggerRange && _timing.CurTime >= comp.NextJumpTime)
            {
                DoJump(uid, comp, target, bossWorld, targetWorld, dist);
            }
        }
    }

    // ── Режим атаки ───────────────────────────────────────────────────────────

    private void UpdateMode(EntityUid uid, DeadMinerComponent comp, float dist)
    {
        var shouldTransform = dist > comp.ModeTransformRange;
        if (shouldTransform == comp.IsTransformed)
            return;

        comp.IsTransformed = shouldTransform;
        _appearance.SetData(uid, DeadMinerVisuals.Transformed, comp.IsTransformed);
        _audio.PlayPvs(comp.TransformSound, uid);

        if (!TryComp<MeleeWeaponComponent>(uid, out var melee))
            return;

        if (comp.IsTransformed)
        {
            melee.AttackRate = comp.MeleeMode2AttackRate;
            var spec = new DamageSpecifier();
            spec.DamageDict.Add("Slash", FixedPoint2.New(comp.MeleeMode2Damage));
            melee.Damage = spec;
        }
        else
        {
            melee.AttackRate = comp.MeleeMode1AttackRate;
            var spec = new DamageSpecifier();
            spec.DamageDict.Add("Slash", FixedPoint2.New(comp.MeleeMode1Damage));
            melee.Damage = spec;
        }

        Dirty(uid, melee);
    }

    // ── Кинетический выстрел ─────────────────────────────────────────────────

    private void FireKinetic(EntityUid uid, DeadMinerComponent comp, Vector2 bossWorld, Vector2 targetWorld)
    {
        var direction = Vector2.Normalize(targetWorld - bossWorld);
        var origin = Transform(uid).Coordinates;

        var bullet = Spawn(comp.KineticBulletPrototype, origin);
        _xformSys.SetWorldRotation(bullet, new Robust.Shared.Maths.Angle(Math.Atan2(direction.Y, direction.X)));

        if (TryComp<ProjectileComponent>(bullet, out var projComp))
        {
            projComp.Shooter = uid;
            Dirty(bullet, projComp);
        }

        _physics.SetLinearVelocity(bullet, direction * comp.KineticProjectileSpeed);
        _audio.PlayPvs(comp.AttackSound, uid);

        comp.NextKineticTime = _timing.CurTime + TimeSpan.FromSeconds(comp.KineticCooldown);
    }

    // ── Прыжок (телепорт) ─────────────────────────────────────────────────────

    private void DoJump(EntityUid uid, DeadMinerComponent comp,
        EntityUid target, Vector2 bossWorld, Vector2 targetWorld, float dist)
    {
        var dir = Vector2.Normalize(targetWorld - bossWorld);

        // Приземляемся перед игроком
        var landDist = dist - MathF.Max(comp.JumpLandDistFromPlayer, 0.5f);
        var landPos = bossWorld + dir * landDist;

        // Дым на месте отправления
        var departCoords = Transform(uid).Coordinates;
        Spawn(comp.SmokePrototype, departCoords);

        // Телепорт: вычисляем смещение в мировых координатах и прибавляем к локальным
        var worldOffset = landPos - bossWorld;
        var landCoords = departCoords.Offset(worldOffset);
        _xformSys.SetCoordinates(uid, landCoords);

        // Остановить движущий импульс HTN после телепорта
        _physics.SetLinearVelocity(uid, Vector2.Zero);

        // Дым на месте приземления
        Spawn(comp.SmokePrototype, Transform(uid).Coordinates);

        _audio.PlayPvs(comp.JumpSound, uid);

        // Popup для ближайших игроков
        foreach (var session in _playerManager.Sessions)
        {
            if (session.Status != SessionStatus.InGame ||
                session.AttachedEntity is not { Valid: true } p) continue;
            if (!TryComp<MobStateComponent>(p, out var ms) || ms.CurrentState != MobState.Alive) continue;
            if (!Transform(uid).Coordinates.TryDistance(EntityManager, Transform(p).Coordinates, out var d) || d > 20f) continue;
            _popup.PopupEntity(Loc.GetString("dead-miner-jump-message"), uid, p, PopupType.Medium);
        }

        comp.NextJumpTime = _timing.CurTime + TimeSpan.FromSeconds(comp.JumpCooldown);
    }

    // ── Вспомогательные ──────────────────────────────────────────────────────

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
