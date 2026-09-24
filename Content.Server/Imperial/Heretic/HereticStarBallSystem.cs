using System.Numerics;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Mobs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Handles the slow-moving star ball projectile for StarBlast (SS13 star_blast.dm).
/// The ball passes through walls (no Physics component) and moves at ~2.5 tiles/sec.
/// </summary>
public sealed class HereticStarBallSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly HereticStatusEffectsSystem _hereticEffects = default!;
    [Dependency] private readonly ThrowingSystem _throw = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;

    private static readonly SoundPathSpecifier ArriveSound =
        new SoundPathSpecifier("/Audio/Imperial/heretic/sound_magic_cosmic_energy.ogg");

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticStarBallComponent>();
        while (query.MoveNext(out var uid, out var ball))
        {
            if (Deleted(ball.CasterUid))
            {
                RemoveActiveBall(ball.CasterUid, uid);
                QueueDel(uid);
                continue;
            }

            var currentPos = _xform.GetWorldPosition(uid);
            var dir = ball.TargetWorldPos - currentPos;
            var dist = dir.Length();

            if (dist < 0.4f || ball.TraveledDistance >= ball.MaxRange)
            {
                OnBallArrive(uid, ball);
                continue;
            }

            var move = Vector2.Normalize(dir) * ball.Speed * frameTime;
            ball.TraveledDistance += move.Length();
            _xform.SetWorldPosition(uid, currentPos + move);
        }
    }

    private void OnBallArrive(EntityUid uid, HereticStarBallComponent ball)
    {
        var casterUid = ball.CasterUid;
        var coords = Transform(uid).Coordinates;

        // SS13 on_hit: primary victim receives burn + knockdown, nearby victims get star mark.
        bool first = true;
        var hitDamage = new DamageSpecifier();
        hitDamage.DamageDict["Heat"] = FixedPoint2.New(20);
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(coords, 3f))
        {
            if (ent.Owner == casterUid) continue;
            _hereticEffects.AddStarMark(ent.Owner, TimeSpan.FromSeconds(30));
            if (first)
            {
                _damage.TryChangeDamage(ent.Owner, hitDamage, ignoreResistances: false);
                _stun.TryKnockdown(ent.Owner, TimeSpan.FromSeconds(4), true);
                first = false;
            }
        }

        Spawn("HereticEffectSpaceExplosion", coords);
        _audio.PlayPvs(ArriveSound, uid);

        RemoveActiveBall(casterUid, uid);
        QueueDel(uid);
    }

    /// <summary>
    /// Recast: pull_victims at ball pos → teleport caster to ball → pull_victims again → delete ball.
    /// Mirrors SS13 star_blast.dm recast logic.
    /// </summary>
    public void RecastBall(EntityUid ballUid, EntityUid casterUid)
    {
        if (!TryComp<HereticStarBallComponent>(ballUid, out _)) return;

        var ballCoords = Transform(ballUid).Coordinates;
        var ballWorldPos = _xform.GetWorldPosition(ballUid);

        // SS13: pull_victims(active_ball.loc) — BEFORE teleport
        PullVictims(casterUid, ballCoords, ballWorldPos);

        // SS13: do_teleport(owner, active_ball)
        _xform.SetCoordinates(casterUid, ballCoords);

        // SS13: pull_victims(owner.loc) — AFTER teleport (same position as ball)
        PullVictims(casterUid, ballCoords, ballWorldPos);

        Spawn("HereticEffectSpaceExplosion", ballCoords);
        _audio.PlayPvs(ArriveSound, casterUid);

        RemoveActiveBall(casterUid, ballUid);
        QueueDel(ballUid);
    }

    private void PullVictims(EntityUid casterUid, EntityCoordinates center, Vector2 centerWorldPos)
    {
        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(center, 2f))
        {
            if (ent.Owner == casterUid) continue;
            Spawn("HereticEffectCosmicBurst", center);
            var victimPos = _xform.GetWorldPosition(ent.Owner);
            var dist = (victimPos - centerWorldPos).Length();
            if (dist <= 1.0f)
            {
                // SS13: range <= 1 → apply star mark
                _hereticEffects.AddStarMark(ent.Owner, TimeSpan.FromSeconds(30));
            }
            else
            {
                // SS13: else → pull one step toward center
                var pullDir = centerWorldPos - victimPos;
                if (pullDir.Length() > 0.01f)
                    _throw.TryThrow(ent.Owner, pullDir.Normalized() * 3f, 3f);
            }
        }
    }

    private void RemoveActiveBall(EntityUid casterUid, EntityUid ballUid)
    {
        if (!TryComp<HereticStarBallActiveComponent>(casterUid, out var active)) return;
        if (active.BallEntity == ballUid)
            RemComp<HereticStarBallActiveComponent>(casterUid);
    }
}
