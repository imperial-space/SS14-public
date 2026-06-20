using Content.Server.Body.Systems;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Lavaland.Weaver;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.Weaver;

public sealed class WeaverSystem : EntitySystem
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _speed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WeaverComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<WeaverComponent, DamageChangedEvent>(OnDamageChanged);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<WeaverComponent, DamageableComponent, MobStateComponent>();
        while (query.MoveNext(out var uid, out var weaver, out var damageable, out var mobState))
        {
            if (mobState.CurrentState != MobState.Alive)
                continue;

            if (!weaver.IsEnraged || now < weaver.NextEatTime)
                continue;

            TryEatNearbyCorpse(uid, weaver, damageable);
        }
    }

    private void OnMeleeHit(Entity<WeaverComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var comp = ent.Comp;
        var reagent = comp.IsEnraged ? comp.RageReagent : comp.NormalReagent;
        var amount = comp.IsEnraged ? comp.RageReagentAmount : comp.NormalReagentAmount;

        foreach (var target in args.HitEntities)
        {
            if (!TryComp<BloodstreamComponent>(target, out var blood))
                continue;

            var solution = new Solution();
            solution.AddReagent(reagent, FixedPoint2.New(amount));
            _bloodstream.TryAddToBloodstream(new Entity<BloodstreamComponent?>(target, blood), solution);
        }

        // Randomize melee damage within range
        if (TryComp<MeleeWeaponComponent>(ent, out var melee))
        {
            var min = comp.IsEnraged ? comp.RageDamageMin : comp.NormalDamageMin;
            var max = comp.IsEnraged ? comp.RageDamageMax : comp.NormalDamageMax;
            var roll = _random.NextFloat(min, max);

            var dmg = new DamageSpecifier();
            dmg.DamageDict["Slash"] = FixedPoint2.New(roll);
            dmg.DamageDict["Piercing"] = FixedPoint2.New(2);

            foreach (var target in args.HitEntities)
                _damageable.TryChangeDamage(target, dmg, ignoreResistances: false);
        }
    }

    private void OnDamageChanged(Entity<WeaverComponent> ent, ref DamageChangedEvent args)
    {
        if (ent.Comp.IsEnraged)
            return;

        if (!TryComp<MobThresholdsComponent>(ent, out var thresholds))
            return;

        var maxHp = 0f;
        foreach (var (value, state) in thresholds.Thresholds)
        {
            if (state == MobState.Dead)
            {
                maxHp = value.Float();
                break;
            }
        }

        if (maxHp <= 0)
            return;

        if (!TryComp<DamageableComponent>(ent, out var damageable))
            return;

#pragma warning disable RA0002
        var totalDamage = damageable.TotalDamage.Float();
#pragma warning restore RA0002
        if (totalDamage / maxHp < (1f - ent.Comp.RageThreshold))
            return;

        Enrage(ent);
    }

    private void Enrage(Entity<WeaverComponent> ent)
    {
        ent.Comp.IsEnraged = true;
        _speed.ChangeBaseSpeed(ent, 2.49f + ent.Comp.RageSpeedBonus, 3.32f + ent.Comp.RageSpeedBonus, 20f);
        _audio.PlayPvs(new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Effects/hiss.ogg"), ent);
    }

    private void TryEatNearbyCorpse(EntityUid uid, WeaverComponent weaver, DamageableComponent damageable)
    {
        var coords = Transform(uid).Coordinates;
        EntityUid? corpse = null;

        foreach (var (mobUid, mobState) in _lookup.GetEntitiesInRange<MobStateComponent>(coords, weaver.CorpseEatRange))
        {
            if (mobState.CurrentState == MobState.Dead && mobUid != uid)
            {
                corpse = mobUid;
                break;
            }
        }

        if (corpse == null)
            return;

        weaver.NextEatTime = _timing.CurTime + weaver.CorpseEatCooldown;

        // Consume the corpse
        QueueDel(corpse.Value);

        // Heal the weaver
        var heal = new DamageSpecifier();
        var currentDamage = _damageable.GetPositiveDamage(new Entity<DamageableComponent>(uid, damageable));
        foreach (var type in currentDamage.DamageDict.Keys)
            heal.DamageDict[type] = -weaver.CorpseEatHeal;

        if (!heal.Empty)
            _damageable.TryChangeDamage(uid, heal, ignoreResistances: true, interruptsDoAfters: false);
    }
}
