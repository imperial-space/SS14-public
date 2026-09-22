using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Heretic.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticFireSharkSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier AttackSound =
        new("/Audio/Imperial/heretic/sound_effects_curse_curse2.ogg");

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticFireSharkComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var shark, out var mobState))
        {
            if (mobState.CurrentState == MobState.Dead)
            {
                Spawn("HereticEffectFireExplosion", Transform(uid).Coordinates);
                QueueDel(uid);
                continue;
            }

            shark.LifetimeElapsed += frameTime;
            if (shark.LifetimeElapsed >= shark.Lifetime)
            {
                Spawn("HereticEffectFireExplosion", Transform(uid).Coordinates);
                QueueDel(uid);
                continue;
            }

            // Passive regen
            shark.RegenTimer += frameTime;
            if (shark.RegenTimer >= shark.RegenInterval)
            {
                shark.RegenTimer = 0f;
                var heal = new DamageSpecifier();
                heal.DamageDict["Blunt"] = FixedPoint2.New(-shark.RegenAmount);
                _damage.TryChangeDamage(uid, heal, ignoreResistances: true);
            }

            // Fire trail
            shark.FireTrailTimer += frameTime;
            if (shark.FireTrailTimer >= shark.FireTrailInterval)
            {
                shark.FireTrailTimer = 0f;
                Spawn("HereticEffectAshBlink", Transform(uid).Coordinates);
            }

            var target = ResolveTarget(uid, shark);
            if (target == null) continue;

            var myPos = _xform.GetWorldPosition(uid);
            var targetPos = _xform.GetWorldPosition(target.Value);
            var dir = targetPos - myPos;
            var dist = dir.Length();

            shark.AttackTimer += frameTime;

            if (dist <= shark.AttackRange)
            {
                if (shark.AttackTimer >= shark.AttackCooldown)
                {
                    shark.AttackTimer = 0f;

                    var dmg = new DamageSpecifier();
                    dmg.DamageDict["Blunt"] = FixedPoint2.New(8);
                    _damage.TryChangeDamage(target.Value, dmg, ignoreResistances: false);
                    _flammable.AdjustFireStacks(target.Value, 3f, ignite: true);
                    _audio.PlayPvs(AttackSound, uid);
                    Spawn("HereticEffectAshBlink", Transform(target.Value).Coordinates);

                    // Bonus burst on marked target — consumes the mark
                    if (HasComp<AshMarkComponent>(target.Value))
                    {
                        var bonusDmg = new DamageSpecifier();
                        bonusDmg.DamageDict["Heat"] = FixedPoint2.New(25);
                        _damage.TryChangeDamage(target.Value, bonusDmg, ignoreResistances: false);
                        _flammable.AdjustFireStacks(target.Value, 5f, ignite: true);
                        Spawn("HereticEffectFireExplosion", Transform(target.Value).Coordinates);
                        RemCompDeferred<AshMarkComponent>(target.Value);
                    }
                }
            }
            else
            {
                var move = Vector2.Normalize(dir) * shark.MoveSpeed * frameTime;
                _xform.SetWorldPosition(uid, myPos + move);
            }
        }
    }

    private EntityUid? ResolveTarget(EntityUid self, HereticFireSharkComponent shark)
    {
        // Prefer the designated target while alive
        if (shark.TargetUid != EntityUid.Invalid && !Deleted(shark.TargetUid))
        {
            if (TryComp<MobStateComponent>(shark.TargetUid, out var ms) && ms.CurrentState != MobState.Dead)
                return shark.TargetUid;
        }

        // Fall back to nearest living mob in range
        EntityUid? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(self).Coordinates, 15f))
        {
            if (ent.Owner == self) continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var ms2)) continue;
            if (ms2.CurrentState == MobState.Dead) continue;

            var dist = (_xform.GetWorldPosition(ent.Owner) - _xform.GetWorldPosition(self)).Length();
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ent.Owner;
            }
        }

        return nearest;
    }
}
