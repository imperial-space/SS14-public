using Content.Shared.CombatMode;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticAmokSystem : EntitySystem
{
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _melee = default!;
    [Dependency] private readonly SharedCombatModeSystem _combatMode = default!;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HereticAmokComponent>();
        while (query.MoveNext(out var uid, out var amok))
        {
            amok.Elapsed += frameTime;
            if (amok.Elapsed >= amok.Duration)
            {
                RemComp<HereticAmokComponent>(uid);
                continue;
            }

            amok.AttackTimer += frameTime;
            if (amok.AttackTimer < amok.AttackCooldown)
                continue;

            var target = FindNearest(uid);
            if (target == null)
                continue;

            amok.AttackTimer = 0f;

            if (!_melee.TryGetWeapon(uid, out var weaponUid, out var weapon))
                continue;

            var combatComp = EnsureComp<CombatModeComponent>(uid);
            var wasCombat = combatComp.IsInCombatMode;
            if (!wasCombat)
                _combatMode.SetInCombatMode(uid, true, combatComp);

            _melee.AttemptLightAttack(uid, weaponUid, weapon, target.Value);

            if (!wasCombat)
                _combatMode.SetInCombatMode(uid, false, combatComp);
        }
    }

    private EntityUid? FindNearest(EntityUid self)
    {
        EntityUid? nearest = null;
        var nearestDist = float.MaxValue;

        foreach (var ent in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(self).Coordinates, 10f))
        {
            if (ent.Owner == self)
                continue;
            if (!TryComp<MobStateComponent>(ent.Owner, out var mob))
                continue;
            if (mob.CurrentState == MobState.Dead)
                continue;

            var dist = (_xform.GetWorldPosition(ent.Owner) - _xform.GetWorldPosition(self)).LengthSquared();
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = ent.Owner;
            }
        }

        return nearest;
    }
}
