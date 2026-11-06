using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Atmos.Components;
using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Paths.Ash;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.NPC.Prototypes;
using Content.Shared.NPC.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Heretic.Paths.Ash;

/// <summary>
/// SS13 ash man (ashen_man.dm): walks through walls, bleeds and burns targets in range,
/// leaves fire trail, despawns after 60 seconds.
/// </summary>
public sealed class HereticAshManSystem : EntitySystem
{
    [Dependency] private readonly Robust.Shared.Timing.IGameTiming _timing = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly DamageableSystem _damage = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;

    private static readonly ProtoId<NpcFactionPrototype> FamiliarFaction = "HereticFamiliar";

    private static readonly SoundPathSpecifier AttackSound =
        new("/Audio/Imperial/heretic/sound_effects_curse_curse2.ogg");

    private static readonly EntProtoId HereticEffectFireExplosion = "HereticEffectFireExplosion";
    private static readonly EntProtoId HereticEffectAshBlink = "HereticEffectAshBlink";

    private const float FindNearestEnemyRange = 15f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticAshManComponent>();
        while (query.MoveNext(out var uid, out var ash))
        {
            if (ash.LifetimeEnd == TimeSpan.Zero)
                ash.LifetimeEnd = _timing.CurTime + ash.Lifetime;

            if (_timing.CurTime >= ash.LifetimeEnd)
            {
                Spawn(HereticEffectFireExplosion, Transform(uid).Coordinates);
                QueueDel(uid);
                continue;
            }

            // Fire trail
            if (_timing.CurTime >= ash.NextFireTrail)
            {
                ash.NextFireTrail = _timing.CurTime + ash.FireTrailInterval;
                Spawn(HereticEffectAshBlink, Transform(uid).Coordinates);
            }

            // Find nearest enemy
            if (_timing.CurTime >= ash.NextRetarget || ash.CachedTarget is { } old && !IsValidTarget(old))
            {
                ash.CachedTarget = FindNearestEnemy(uid, ash.CasterUid);
                ash.NextRetarget = _timing.CurTime + RetargetInterval;
            }

            var target = ash.CachedTarget;
            if (target == null)
                continue;

            var myPos = _xform.GetWorldPosition(uid);
            var targetPos = _xform.GetWorldPosition(target.Value);
            var dir = targetPos - myPos;
            var dist = dir.Length();

            if (dist <= ash.AttackRange)
            {
                // In range: deal damage
                if (_timing.CurTime >= ash.NextAttack)
                {
                    ash.NextAttack = _timing.CurTime + ash.AttackCooldown;
                    var dmg = new DamageSpecifier(ash.UpdateDamage);
                    _damage.TryChangeDamage(target.Value, dmg, ignoreResistances: false);
                    if (HasComp<FlammableComponent>(target.Value))
                        _flammable.AdjustFireStacks(target.Value, 3f, ignite: true);
                    _audio.PlayPvs(AttackSound, uid);
                    Spawn(HereticEffectAshBlink, Transform(target.Value).Coordinates);
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

    private static readonly TimeSpan RetargetInterval = TimeSpan.FromSeconds(0.5);

    private bool IsValidTarget(EntityUid target)
    {
        return !TerminatingOrDeleted(target) && TryComp<MobStateComponent>(target, out var mob) && mob.CurrentState != MobState.Dead;
    }

    private EntityUid? FindNearestEnemy(EntityUid self, EntityUid? caster)
    {
        EntityUid? nearest = null;
        float nearestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(self).Coordinates, FindNearestEnemyRange))
        {
            if (ent.Owner == self || ent.Owner == caster)
                continue;
            if (IsAlly(ent.Owner))
                continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var mobState))
                continue;
            if (mobState.CurrentState == MobState.Dead)
                continue;

            var dist = (_xform.GetWorldPosition(ent.Owner) - _xform.GetWorldPosition(self)).Length();
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ent.Owner;
            }
        }

        return nearest;
    }

    /// <summary>
    /// Еретики и их фамильяры — свои: миньоны пепла их не атакуют.
    /// </summary>
    private bool IsAlly(EntityUid uid)
    {
        return HasComp<HereticComponent>(uid)
            || HasComp<HereticAshManComponent>(uid)
            || HasComp<HereticFireSharkComponent>(uid)
            || _faction.IsMember(uid, FamiliarFaction);
    }
}
