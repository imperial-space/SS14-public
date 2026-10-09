using System.Linq;
using System.Numerics;
using Content.Server.Imperial.Lavaland.Megafauna;
using Content.Server.Imperial.Lavaland.MegafaunaTracker;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Damage.Systems;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.NPC.Systems;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Content.Shared.Weapons.Ranged.Events;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Lavaland.OrePoints.Mining.Systems;

/// <summary>
/// item/kinetic_crusher: дестабилизатор, метка, подрыв метки, трофеи и crusher_loot.
/// </summary>
public sealed partial class KineticCrusherSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly KineticAcceleratorSystem _accelerator = default!;
    [Dependency] private readonly KineticEquipmentSystem _equipment = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;

    private static readonly ProtoId<DamageTypePrototype> Blunt = "Blunt";
    private static readonly ProtoId<DamageTypePrototype> Heat = "Heat";
    private static readonly ProtoId<DamageGroupPrototype>[] DemonHealOrder = { "Brute", "Burn", "Airloss" };
    private static readonly EntProtoId FireEffect = "CrusherTailSpikeFireEffect";
    private static readonly EntProtoId ChaserBlast = "ImperialHierophantBlastDamaging";
    private static readonly SoundSpecifier FireballSound = new SoundPathSpecifier("/Audio/Magic/fireball.ogg");
    private static readonly SoundSpecifier ChaserBlastSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_magic_blind.ogg");
    private static readonly SoundSpecifier ChaserHitSound = new SoundPathSpecifier("/Audio/Imperial/boss/sound_weapons_sear.ogg");
    private static readonly Vector2i[] Cardinals = { new(0, 1), new(0, -1), new(1, 0), new(-1, 0) };

    /// <summary>projectile/destabilizer range.</summary>
    private const int DestabilizerRange = 6;

    private sealed class Chaser
    {
        public EntityUid User;
        public EntityUid Target;
        public Entity<MapGridComponent> Grid;
        public Vector2i Pos;
        public Vector2i MovingDir;
        public Vector2i PreviousDir;
        public Vector2i MorePreviousDir;
        public int Moving;
        public TimeSpan NextStep;
        public TimeSpan End;
    }

    private readonly List<Chaser> _chasers = new();
    private readonly List<(TimeSpan At, Entity<MapGridComponent> Grid, Vector2i Tile, EntityUid User, HashSet<EntityUid> Hit, bool Last)> _blasts = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<KineticCrusherComponent, GunShotEvent>(OnGunShot);
        SubscribeLocalEvent<KineticCrusherComponent, MeleeHitEvent>(OnMeleeHit);
        SubscribeLocalEvent<CrusherDestabilizerComponent, ProjectileHitEvent>(OnDestabilizerHit);
        SubscribeLocalEvent<CrusherLootComponent, MobStateChangedEvent>(OnLootMobState);
        SubscribeLocalEvent<CrusherRangedDelayComponent, ShotAttemptedEvent>(OnRangedDelayShot);

        SubscribeLocalEvent<CrusherBloodDrunkComponent, DamageModifyEvent>(OnBloodDrunkDamage);
        SubscribeLocalEvent<CrusherBloodDrunkComponent, BeforeStatusEffectAddedEvent>(OnBloodDrunkStatus);
        SubscribeLocalEvent<CrusherBloodDrunkComponent, KnockDownAttemptEvent>(OnBloodDrunkKnockdown);

        InitializeTrophies();
    }

    #region Дестабилизатор

    /// <summary>fire_kinetic_blast(): on_projectile_fire() трофеев.</summary>
    private void OnGunShot(Entity<KineticCrusherComponent> ent, ref GunShotEvent args)
    {
        var speed = TryComp<GunComponent>(ent, out var gun) ? gun.ProjectileSpeedModified : 25f;

        foreach (var (uid, _) in args.Ammo)
        {
            if (uid is not { } projectile)
                continue;

            var destabilizer = EnsureComp<CrusherDestabilizerComponent>(projectile);
            destabilizer.Crusher = ent;
            destabilizer.Firer = args.User;
            destabilizer.IgnoreAllies = _equipment.HasTrophy(ent, CrusherTrophyKind.RaptorFeather);

            if (TryComp<TimedDespawnComponent>(projectile, out var despawn) && speed > 0f)
                despawn.Lifetime = DestabilizerRange / speed;

            foreach (var trophy in _equipment.GetTrophies(ent))
            {
                if (!trophy.Comp.DeadlyShot)
                    continue;
                if (trophy.Comp.Trophy is not (CrusherTrophyKind.BlasterTubes or CrusherTrophyKind.MagmaWing))
                    continue;

                trophy.Comp.DeadlyShot = false;
                if (TryComp<ProjectileComponent>(projectile, out var proj))
                    proj.Damage = new DamageSpecifier { DamageDict = { [Blunt] = trophy.Comp.BonusValue } };

                // blaster_tubes: speed = 2 — снаряд вдвое медленнее и летит вдвое дольше.
                if (trophy.Comp.Trophy == CrusherTrophyKind.BlasterTubes)
                {
                    destabilizer.Slow = true;
                    if (despawn != null)
                        despawn.Lifetime *= 2f;
                }
            }
        }
    }

    /// <summary>projectile/destabilizer/on_hit(): метит живую цель.</summary>
    private void OnDestabilizerHit(Entity<CrusherDestabilizerComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;
        if (!HasComp<MobStateComponent>(target))
        {
            OnDestabilizerHitMineral(ent, target);
            return;
        }

        // Структурный урон магмитового заряда по существам не проходит.
        var shotDamage = args.Damage.DamageDict.Where(d => d.Key != "Structural").Sum(d => d.Value.Float());
        AddCrusherDamage(target, shotDamage);

        // status_effect/crusher_mark/on_apply(): только mob_size >= MOB_SIZE_LARGE.
        if (!IsLargeMob(target) || ent.Comp.Crusher is not { } crusherUid || !TryComp<KineticCrusherComponent>(crusherUid, out var crusher))
            return;

        var mark = EnsureComp<CrusherMarkComponent>(target);
        mark.ExpiresAt = _timing.CurTime + crusher.MarkDuration;
        Dirty(target, mark);
    }

    #endregion

    #region Ближний бой

    /// <summary>attack() + afterattack(): трофеи on_melee_hit и подрыв метки.</summary>
    private void OnMeleeHit(Entity<KineticCrusherComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        var user = args.User;
        var baseDamage = args.BaseDamage.GetTotal().Float();

        foreach (var target in args.HitEntities.ToList())
        {
            if (!HasComp<MobStateComponent>(target))
                continue;

            AddCrusherDamage(target, baseDamage);

            foreach (var trophy in _equipment.GetTrophies(ent))
            {
                if (trophy.Comp.Trophy == CrusherTrophyKind.DemonClaws)
                    _accelerator.HealOrdered(user, trophy.Comp.BonusValue * 0.1f, DemonHealOrder);
                else if (trophy.Comp.Trophy == CrusherTrophyKind.FleshGlob)
                    _accelerator.HealOrdered(user, trophy.Comp.BonusValue * 0.2f, FleshHealOrder);
            }

            if (!TryComp<CrusherMarkComponent>(target, out var mark) || mark.ExpiresAt < _timing.CurTime)
                continue;

            RemComp<CrusherMarkComponent>(target);
            Detonate(ent, user, target);
        }
    }

    private void Detonate(Entity<KineticCrusherComponent> ent, EntityUid user, EntityUid target)
    {
        var combined = _equipment.GetDetonationDamage(ent);
        foreach (var trophy in _equipment.GetTrophies(ent).ToList())
        {
            combined += OnMarkDetonation(ent, trophy, user, target);
        }

        if (TerminatingOrDeleted(target))
            return;

        Spawn(ent.Comp.DetonationEffect, Transform(target).Coordinates);

        if (IsBackstab(user, target))
        {
            combined += ent.Comp.BackstabBonus;
            _audio.PlayPvs(ent.Comp.BackstabSound, user, AudioParams.Default.WithVolume(5f));
        }

        if (_damageable.TryChangeDamage(target, new DamageSpecifier { DamageDict = { [Blunt] = combined } }, out var dealt, origin: user))
            AddCrusherDamage(target, dealt.GetTotal().Float());
    }

    /// <summary>crusher_trophy/on_mark_detonation(): возвращает добавочный урон.</summary>
    private float OnMarkDetonation(Entity<KineticCrusherComponent> crusher, Entity<CrusherTrophyComponent> trophy, EntityUid user, EntityUid target)
    {
        var bonus = trophy.Comp.BonusValue;
        switch (trophy.Comp.Trophy)
        {
            case CrusherTrophyKind.WatcherWing:
                // ranged_cooldown += bonus_value (децисекунды).
                if (HasComp<GunComponent>(target))
                {
                    var delay = TimeSpan.FromSeconds(bonus * 0.1f);
                    var rangedDelay = EnsureComp<CrusherRangedDelayComponent>(target);
                    rangedDelay.Until = rangedDelay.Until > _timing.CurTime ? rangedDelay.Until + delay : _timing.CurTime + delay;
                }
                break;

            case CrusherTrophyKind.MagmaWing:
            case CrusherTrophyKind.BlasterTubes:
                trophy.Comp.DeadlyShot = true;
                trophy.Comp.DeadlyShotReset = _timing.CurTime + TimeSpan.FromSeconds(30);
                break;

            case CrusherTrophyKind.GoliathTentacle:
                // (maxHealth - health) * 0.1 * bonus_value.
                if (TryComp<DamageableComponent>(user, out var userDamage))
                {
                    var missing = _damageable.GetTotalDamage((user, userDamage)).Float() * 0.1f * bonus;
                    if (missing > 0f)
                        return missing;
                }
                break;

            case CrusherTrophyKind.MinerEye:
                var drunk = EnsureComp<CrusherBloodDrunkComponent>(user);
                drunk.ExpiresAt = _timing.CurTime + TimeSpan.FromSeconds(1);
                break;

            case CrusherTrophyKind.TailSpike:
                TailSpike(user, bonus);
                break;

            case CrusherTrophyKind.DemonClaws:
                _accelerator.HealOrdered(user, bonus * 0.4f, DemonHealOrder);
                break;

            case CrusherTrophyKind.VortexTalisman:
                StartChaser(user, target);
                break;

            default:
                OnMarkDetonationExtra(crusher, trophy, user, target);
                break;
        }

        return 0f;
    }

    /// <summary>tail_spike: огонь по всем в oview(2) и отталкивание.</summary>
    private void TailSpike(EntityUid user, float damage)
    {
        var userPos = _transform.GetWorldPosition(user);
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(Transform(user).Coordinates, 2.5f).ToList())
        {
            if (mob == user || _mobState.IsDead(mob) || IsCrusherAlly(user, mob))
                continue;

            _audio.PlayPvs(FireballSound, mob, AudioParams.Default.WithVolume(-8f).WithVariation(0.05f));
            Spawn(FireEffect, Transform(mob).Coordinates);
            _damageable.TryChangeDamage(mob, new DamageSpecifier { DamageDict = { [Heat] = damage } }, ignoreResistances: true, origin: user);

            var dir = _transform.GetWorldPosition(mob) - userPos;
            if (dir.LengthSquared() > 0.0001f && (!Transform(mob).Anchored || IsMegafauna(mob)))
                _throwing.TryThrow(mob, Vector2.Normalize(dir), 5f, user, recoil: false, doSpin: false);
        }
    }

    private bool IsBackstab(EntityUid attacker, EntityUid target)
    {
        var toAttacker = _transform.GetWorldPosition(attacker) - _transform.GetWorldPosition(target);
        if (toAttacker.LengthSquared() < 0.0001f)
            return false;

        var facing = _transform.GetWorldRotation(target).ToWorldVec();
        return Vector2.Dot(facing, Vector2.Normalize(toAttacker)) < -0.5f;
    }

    #endregion

    #region Вихревой талисман

    /// <summary>temp_visual/hierophant/chaser: speed 3, урон 20, без бонуса по монстрам.</summary>
    private void StartChaser(EntityUid user, EntityUid target)
    {
        var xform = Transform(user);
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return;

        _chasers.Add(new Chaser
        {
            User = user,
            Target = target,
            Grid = (gridUid, grid),
            Pos = _map.TileIndicesFor(gridUid, grid, xform.Coordinates),
            NextStep = _timing.CurTime + TimeSpan.FromSeconds(0.1),
            End = _timing.CurTime + TimeSpan.FromSeconds(9.8),
        });
    }

    private Vector2i CardinalDir(Vector2i from, Vector2i to)
    {
        var dx = Math.Abs(to.X - from.X);
        var dy = Math.Abs(to.Y - from.Y);
        if (dx == 0 && dy == 0)
            return Vector2i.Zero;
        return _random.NextFloat() * (dx + dy) < dy
            ? new Vector2i(0, Math.Sign(to.Y - from.Y))
            : new Vector2i(Math.Sign(to.X - from.X), 0);
    }

    private bool StepChaser(Chaser c, TimeSpan now)
    {
        if (now >= c.End || TerminatingOrDeleted(c.Target) || Transform(c.Target).GridUid != c.Grid.Owner)
            return false;

        var targetTile = _map.TileIndicesFor(c.Grid.Owner, c.Grid.Comp, Transform(c.Target).Coordinates);
        if (c.Moving <= 0)
        {
            c.MorePreviousDir = c.PreviousDir;
            c.PreviousDir = c.MovingDir;
            var dir = CardinalDir(c.Pos, targetTile);
            if (dir != c.PreviousDir && dir == c.MorePreviousDir || dir == Vector2i.Zero)
                dir = _random.Pick(Cardinals.Where(d => d != c.MorePreviousDir).ToList());
            c.MovingDir = dir;

            var standard = CardinalDir(c.Pos, targetTile);
            c.Moving = standard != c.PreviousDir && standard == c.MorePreviousDir || standard == Vector2i.Zero ? 1 : 4;
        }

        c.Pos += c.MovingDir;
        c.Moving--;

        var coords = _map.GridTileToLocal(c.Grid.Owner, c.Grid.Comp, c.Pos);
        Spawn(ChaserBlast, coords);
        _audio.PlayPvs(ChaserBlastSound, coords, AudioParams.Default.WithVolume(-5f));
        // blast/damaging: удар через 0.6 с и ещё раз через 0.13 с по вошедшим.
        var hit = new HashSet<EntityUid> { c.User };
        _blasts.Add((now + TimeSpan.FromSeconds(0.6), c.Grid, c.Pos, c.User, hit, false));
        _blasts.Add((now + TimeSpan.FromSeconds(0.73), c.Grid, c.Pos, c.User, hit, true));

        c.NextStep = now + TimeSpan.FromSeconds(0.3);
        return true;
    }

    private void BlastDamage(Entity<MapGridComponent> grid, Vector2i tile, EntityUid user, HashSet<EntityUid> hit)
    {
        var center = _map.GridTileToLocal(grid.Owner, grid.Comp, tile);
        foreach (var (mob, _) in _lookup.GetEntitiesInRange<MobStateComponent>(center, 0.45f).ToList())
        {
            if (!hit.Add(mob) || _mobState.IsDead(mob) || IsCrusherAlly(user, mob))
                continue;

            _audio.PlayPvs(ChaserHitSound, mob, AudioParams.Default.WithVolume(-5f));
            _damageable.TryChangeDamage(mob, new DamageSpecifier { DamageDict = { [Heat] = 20 } }, origin: user);
        }
    }

    #endregion

    #region Добыча трофеев

    private void AddCrusherDamage(EntityUid target, float amount)
    {
        if (amount <= 0f)
            return;

        EnsureComp<CrusherDamageComponent>(target).TotalDamage += amount;
    }

    /// <summary>element/crusher_loot/on_death().</summary>
    private void OnLootMobState(Entity<CrusherLootComponent> ent, ref MobStateChangedEvent args)
    {
        if (args.NewMobState != MobState.Dead || !TryComp<CrusherDamageComponent>(ent, out var damage))
            return;

        if (!_thresholds.TryGetDeadThreshold(ent, out var maxHealth) || maxHealth <= FixedPoint2.Zero)
            return;

        var fraction = damage.TotalDamage / maxHealth.Value.Float();
        RemComp<CrusherDamageComponent>(ent);

        if (ent.Comp.GuaranteedDrop is { } guaranteed)
        {
            if (fraction < guaranteed)
                return;
        }
        else if (!_random.Prob(Math.Clamp(fraction * ent.Comp.DropChance / 100f, 0f, 1f)))
        {
            return;
        }

        var coords = Transform(ent).Coordinates;
        foreach (var trophy in ent.Comp.Trophies)
        {
            Spawn(trophy, coords);
        }
    }

    #endregion

    private void OnRangedDelayShot(Entity<CrusherRangedDelayComponent> ent, ref ShotAttemptedEvent args)
    {
        if (ent.Comp.Until > _timing.CurTime)
            args.Cancel();
    }

    #region Blooddrunk

    /// <summary>status_effect/blooddrunk: урон ×0.1.</summary>
    private void OnBloodDrunkDamage(Entity<CrusherBloodDrunkComponent> ent, ref DamageModifyEvent args)
    {
        if (ent.Comp.ExpiresAt > _timing.CurTime)
            args.Damage *= 0.1f;
    }

    /// <summary>status_effect/blooddrunk: иммунитет к оглушению.</summary>
    private void OnBloodDrunkStatus(Entity<CrusherBloodDrunkComponent> ent, ref BeforeStatusEffectAddedEvent args)
    {
        if (ent.Comp.ExpiresAt > _timing.CurTime && args.Effect == SharedStunSystem.StunId)
            args.Cancelled = true;
    }

    private void OnBloodDrunkKnockdown(Entity<CrusherBloodDrunkComponent> ent, ref KnockDownAttemptEvent args)
    {
        if (ent.Comp.ExpiresAt > _timing.CurTime)
            args.Cancelled = true;
    }

    #endregion

    #region Помощники

    /// <summary>mob_size >= MOB_SIZE_LARGE: существа Лаваленда, но не гуманоиды (пеплоходцы).</summary>
    public bool IsLargeMob(EntityUid uid)
    {
        return !HasComp<HumanoidProfileComponent>(uid) && _accelerator.IsMiningMob(uid);
    }

    private bool IsMegafauna(EntityUid uid)
    {
        return HasComp<LavalandMegafaunaComponent>(uid) || HasComp<MegafaunaAiComponent>(uid);
    }

    #endregion

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var marks = EntityQueryEnumerator<CrusherMarkComponent>();
        while (marks.MoveNext(out var uid, out var mark))
        {
            if (now >= mark.ExpiresAt)
                RemCompDeferred<CrusherMarkComponent>(uid);
        }

        UpdateTrophies(now);

        var drunk = EntityQueryEnumerator<CrusherBloodDrunkComponent>();
        while (drunk.MoveNext(out var uid, out var comp))
        {
            if (now >= comp.ExpiresAt)
                RemCompDeferred<CrusherBloodDrunkComponent>(uid);
        }

        var trophies = EntityQueryEnumerator<CrusherTrophyComponent>();
        while (trophies.MoveNext(out _, out var trophy))
        {
            if (trophy.DeadlyShot && now >= trophy.DeadlyShotReset)
                trophy.DeadlyShot = false;
        }

        var shots = EntityQueryEnumerator<CrusherDestabilizerComponent, PhysicsComponent>();
        while (shots.MoveNext(out var uid, out var shot, out var physics))
        {
            if (!shot.Slow || shot.Slowed || physics.LinearVelocity.LengthSquared() <= 0f)
                continue;

            shot.Slowed = true;
            _physics.SetLinearVelocity(uid, physics.LinearVelocity * 0.5f, body: physics);
        }

        for (var i = _chasers.Count - 1; i >= 0; i--)
        {
            var chaser = _chasers[i];
            if (now < chaser.NextStep)
                continue;
            if (!StepChaser(chaser, now))
                _chasers.RemoveAt(i);
        }

        for (var i = _blasts.Count - 1; i >= 0; i--)
        {
            var blast = _blasts[i];
            if (now < blast.At)
                continue;
            _blasts.RemoveAt(i);
            if (!TerminatingOrDeleted(blast.Grid))
                BlastDamage(blast.Grid, blast.Tile, blast.User, blast.Hit);
        }
    }
}

/// <summary>projectile/destabilizer.</summary>
[RegisterComponent]
public sealed partial class CrusherDestabilizerComponent : Component
{
    [ViewVariables]
    public EntityUid? Crusher;

    [ViewVariables]
    public EntityUid? Firer;

    [ViewVariables]
    public bool Slow;

    [ViewVariables]
    public bool Slowed;

    /// <summary>raptor_feather: ignore_allies.</summary>
    [ViewVariables]
    public bool IgnoreAllies;
}

/// <summary>watcher_wing: ranged_cooldown цели.</summary>
[RegisterComponent]
public sealed partial class CrusherRangedDelayComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}

/// <summary>status_effect/blooddrunk.</summary>
[RegisterComponent]
public sealed partial class CrusherBloodDrunkComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
