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

/// <summary>
/// SS13 ash man (ashen_man.dm): walks through walls, bleeds and burns targets in range,
/// leaves fire trail, despawns after 60 seconds.
/// </summary>
public sealed class HereticAshManSystem : EntitySystem
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

        var query = EntityQueryEnumerator<HereticAshManComponent>();
        while (query.MoveNext(out var uid, out var ash))
        {
            ash.LifetimeElapsed += frameTime;
            if (ash.LifetimeElapsed >= ash.Lifetime)
            {
                Spawn("HereticEffectFireExplosion", Transform(uid).Coordinates);
                QueueDel(uid);
                continue;
            }

            // Fire trail
            ash.FireTrailTimer += frameTime;
            if (ash.FireTrailTimer >= ash.FireTrailInterval)
            {
                ash.FireTrailTimer = 0f;
                Spawn("HereticEffectAshBlink", Transform(uid).Coordinates);
            }

            // Find nearest enemy
            var target = FindNearestEnemy(uid, ash.CasterUid);
            if (target == null) continue;

            var myPos = _xform.GetWorldPosition(uid);
            var targetPos = _xform.GetWorldPosition(target.Value);
            var dir = targetPos - myPos;
            var dist = dir.Length();

            ash.AttackTimer += frameTime;

            if (dist <= ash.AttackRange)
            {
                // In range: deal damage
                if (ash.AttackTimer >= ash.AttackCooldown)
                {
                    ash.AttackTimer = 0f;
                    var dmg = new DamageSpecifier();
                    // SS13 ash man: slash + heat + brute (bleed)
                    dmg.DamageDict["Slash"] = FixedPoint2.New(10);
                    dmg.DamageDict["Heat"] = FixedPoint2.New(8);
                    dmg.DamageDict["Blunt"] = FixedPoint2.New(5);
                    _damage.TryChangeDamage(target.Value, dmg, ignoreResistances: false);
                    _flammable.AdjustFireStacks(target.Value, 3f, ignite: true);
                    _audio.PlayPvs(AttackSound, uid);
                    Spawn("HereticEffectAshBlink", Transform(target.Value).Coordinates);
                }
            }
            else
            {
                // Move directly toward target (through walls — no navmesh)
                var move = Vector2.Normalize(dir) * ash.MoveSpeed * frameTime;
                _xform.SetWorldPosition(uid, myPos + move);
            }
        }
    }

    private EntityUid? FindNearestEnemy(EntityUid self, EntityUid caster)
    {
        EntityUid? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(self).Coordinates, 15f))
        {
            if (ent.Owner == self || ent.Owner == caster) continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var mobState)) continue;
            if (mobState.CurrentState == MobState.Dead) continue;

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
