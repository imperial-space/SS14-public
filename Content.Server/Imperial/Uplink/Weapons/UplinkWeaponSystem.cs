using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Armor;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Imperial.Uplink.Weapons;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Projectiles;
using Content.Shared.StatusEffectNew;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Content.Shared.Tools;
using Content.Shared.Tools.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using Content.Shared.Verbs;
using Content.Shared.Database;

namespace Content.Server.Imperial.Uplink.Weapons;

/// <summary>
/// Механики оружия аплинка TG: бронепробитие и эффекты пуль, самонаведение, силовой кулак.
/// </summary>
public sealed class UplinkWeaponSystem : EntitySystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly GasTankSystem _gasTank = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly Content.Shared.Body.Systems.SharedBloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly SharedEmpSystem _emp = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly StatusEffectsSystem _status = default!;
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly Content.Server.Explosion.EntitySystems.ExplosionSystem _explosion = default!;
    [Dependency] private readonly Content.Server.Administration.Logs.IAdminLogManager _adminLog = default!;

    private static readonly ProtoId<ToolQualityPrototype> WrenchQuality = "Anchoring";
    private static readonly SoundSpecifier FistBlastSound = new SoundPathSpecifier("/Audio/Weapons/genhit2.ogg");
    private static readonly SoundSpecifier FistWeakSound = new SoundPathSpecifier("/Audio/Weapons/punch4.ogg");
    private static readonly SoundSpecifier EvidenceBombArmSound = new SoundPathSpecifier("/Audio/Imperial/Uplink/armbomb.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<UplinkProjectileComponent, ProjectileHitEvent>(OnProjectileHit);

        SubscribeLocalEvent<PowerFistComponent, MeleeHitEvent>(OnFistHit);
        SubscribeLocalEvent<PowerFistComponent, InteractUsingEvent>(OnFistInteractUsing);
        SubscribeLocalEvent<PowerFistComponent, ExaminedEvent>(OnFistExamined);

        SubscribeLocalEvent<UplinkEvidenceBombComponent, GetVerbsEvent<AlternativeVerb>>(OnEvidenceBombVerbs);
        SubscribeLocalEvent<UplinkEvidenceBombComponent, ExaminedEvent>(OnEvidenceBombExamined);
        SubscribeLocalEvent<UplinkSmartgunComponent, Content.Shared.Weapons.Ranged.Events.AmmoShotEvent>(OnSmartgunShot);
    }

    #region Пули

    private void OnProjectileHit(Entity<UplinkProjectileComponent> ent, ref ProjectileHitEvent args)
    {
        var target = args.Target;

        if (ent.Comp.ArmorPenetration > 0f || ent.Comp.WeakAgainstArmor)
            args.Damage = AdjustForArmor(target, args.Damage, ent.Comp.ArmorPenetration, ent.Comp.WeakAgainstArmor);

        var isMob = HasComp<MobStateComponent>(target);

        if (ent.Comp.FireStacks > 0f && isMob)
            _flammable.AdjustFireStacks(target, ent.Comp.FireStacks, ignite: true);

        if (ent.Comp.HotspotRadius >= 0)
            SpawnHotspots(target, ent.Comp.HotspotRadius);

        if (isMob)
        {
            // Длительности в прототипах — из TG, переводятся в нормы контроля SS14.
            if (ent.Comp.Sleep > TimeSpan.Zero)
                _status.TryAddStatusEffectDuration(target, SleepingSystem.StatusEffectForcedSleeping, Content.Shared.Imperial.Balance.ImperialControlNorms.Sleep(ent.Comp.Sleep));

            if (ent.Comp.Stun > TimeSpan.Zero)
                _stun.TryAddStunDuration(target, Content.Shared.Imperial.Balance.ImperialControlNorms.HardStun(ent.Comp.Stun));

            if (ent.Comp.Knockdown > TimeSpan.Zero)
                _stun.TryKnockdown(target, Content.Shared.Imperial.Balance.ImperialControlNorms.Knockdown(ent.Comp.Knockdown));

            if (ent.Comp.Bleed > 0f)
                _bloodstream.TryModifyBleedAmount(target, ent.Comp.Bleed);

            if (ent.Comp.BloodLoss > 0f)
                _bloodstream.TryModifyBloodLevel(target, -ent.Comp.BloodLoss);
        }

        if (ent.Comp.StructureExplosion && !isMob)
            _explosion.QueueExplosion(_transform.GetMapCoordinates(target), "Default", 20f, 10f, 10f, ent, canCreateVacuum: false);

        if (ent.Comp.EmpRange > 0f)
            _emp.EmpPulse(Transform(target).Coordinates, ent.Comp.EmpRange, ent.Comp.EmpEnergy, ent.Comp.EmpDuration);
    }

    /// <summary>
    /// Броня SS14 применяется после этого события, поэтому заранее масштабируем урон так,
    /// чтобы после неё вышло: AP — броня ослаблена на долю пробития, weak — броня вдвое сильнее.
    /// </summary>
    private DamageSpecifier AdjustForArmor(EntityUid target, DamageSpecifier damage, float penetration, bool weak)
    {
        var query = new CoefficientQueryEvent(~SlotFlags.POCKET);
        RaiseLocalEvent(target, query);

        var result = new DamageSpecifier(damage);
        foreach (var (type, amount) in damage.DamageDict)
        {
            if (!query.DamageModifiers.Coefficients.TryGetValue(type, out var coefficient) || coefficient >= 1f || coefficient <= 0f)
                continue;

            var armor = 1f - coefficient;
            var wanted = weak
                ? MathF.Max(0f, 1f - armor * 2f)
                : 1f - armor * (1f - Math.Clamp(penetration, 0f, 1f));

            result.DamageDict[type] = amount * (wanted / coefficient);
        }

        return result;
    }

    private void SpawnHotspots(EntityUid target, int radius)
    {
        var xform = Transform(target);
        if (xform.GridUid is not { } grid)
            return;

        var center = _transform.GetGridOrMapTilePosition(target, xform);
        for (var x = -radius; x <= radius; x++)
        {
            for (var y = -radius; y <= radius; y++)
            {
                _atmos.HotspotExpose(grid, center + new Vector2i(x, y), 700f, 50f, target, true);
            }
        }
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        UpdateEvidenceBombs();

        var query = EntityQueryEnumerator<UplinkHomingProjectileComponent, ProjectileComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var homing, out var projectile, out var physics, out var xform))
        {
            if (!homing.TargetResolved)
            {
                homing.TargetResolved = true;
                homing.Target = ResolveHomingTarget(projectile);
            }

            if (homing.Target is not { } target || TerminatingOrDeleted(target))
                continue;

            var velocity = physics.LinearVelocity;
            var speed = velocity.Length();
            if (speed <= 0.01f)
                continue;

            var ourPos = _transform.GetWorldPosition(xform);
            var targetPos = _transform.GetWorldPosition(target);
            targetPos += homing.Offset;
            if (homing.Inaccuracy > 0f)
                targetPos += _random.NextVector2(homing.Inaccuracy);

            var wanted = targetPos - ourPos;
            if (wanted.LengthSquared() < 0.01f)
                continue;

            var current = new Angle(velocity);
            var desired = new Angle(wanted);
            var diff = Angle.ShortestDistance(current, desired);
            var maxTurn = Angle.FromDegrees(homing.TurnSpeed * frameTime);
            var turn = Math.Clamp(diff.Theta, -maxTurn.Theta, maxTurn.Theta);
            var newAngle = current + new Angle(turn);

            _physics.SetLinearVelocity(uid, newAngle.ToVec() * speed, body: physics);
            _transform.SetWorldRotation(uid, newAngle.ToVec().ToWorldAngle() + projectile.Angle);
        }
    }

    /// <summary>
    /// c160smart/ready_proj(): цель фиксируется в момент выстрела, пока у оружия ещё есть точка прицела.
    /// Клик не по полу — наводимся на кликнутое; по полу — на живое существо в радиусе auto_lock_range.
    /// </summary>
    private void OnSmartgunShot(EntityUid uid, UplinkSmartgunComponent comp, Content.Shared.Weapons.Ranged.Events.AmmoShotEvent args)
    {
        Entity<UplinkSmartgunComponent> ent = (uid, comp);
        if (!TryComp<GunComponent>(ent, out var gun))
            return;

        var shooter = Transform(ent).ParentUid;
        EntityUid? target = gun.Target is { } clicked && clicked != shooter && !TerminatingOrDeleted(clicked) ? clicked : null;
        if (target == null && gun.ShootCoordinates is { } coords)
        {
            var mapCoords = _transform.ToMapCoordinates(coords);
            var bestDist = float.MaxValue;
            foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(mapCoords, ent.Comp.AutoLockRange))
            {
                if (mob.Owner == shooter)
                    continue;

                var dist = (_transform.GetWorldPosition(mob) - mapCoords.Position).LengthSquared();
                if (dist < bestDist)
                {
                    bestDist = dist;
                    target = mob;
                }
            }
        }

        if (target is not { } locked)
            return;

        foreach (var projectile in args.FiredProjectiles)
        {
            if (!TryComp<UplinkHomingProjectileComponent>(projectile, out var homing))
                continue;

            homing.Target = locked;
            homing.TargetResolved = true;
            // homing_offset_x/y = rand(homing_inaccuracy_min, homing_inaccuracy_max)
            homing.Offset = new Vector2(_random.NextFloat(homing.InaccuracyMin, homing.InaccuracyMax), _random.NextFloat(homing.InaccuracyMin, homing.InaccuracyMax));
        }

        Spawn(ent.Comp.Reticle, Transform(locked).Coordinates);
    }

    /// <summary>Цель — то, на что кликнул стрелок; иначе ближайшее живое существо у точки выстрела.</summary>
    private EntityUid? ResolveHomingTarget(ProjectileComponent projectile)
    {
        if (!TryComp<GunComponent>(projectile.Weapon, out var gun))
            return null;

        if (gun.Target is { } target && HasComp<MobStateComponent>(target) && target != projectile.Shooter)
            return target;

        if (gun.ShootCoordinates is not { } coords)
            return null;

        EntityUid? best = null;
        var bestDist = float.MaxValue;
        var mapCoords = _transform.ToMapCoordinates(coords);
        foreach (var mob in _lookup.GetEntitiesInRange<MobStateComponent>(mapCoords, 1.5f))
        {
            if (mob.Owner == projectile.Shooter)
                continue;

            var dist = (_transform.GetWorldPosition(mob) - mapCoords.Position).LengthSquared();
            if (dist < bestDist)
            {
                bestDist = dist;
                best = mob;
            }
        }

        return best;
    }

    #endregion

    #region Бомба уничтожения улик

    private void OnEvidenceBombExamined(Entity<UplinkEvidenceBombComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("uplink-evidence-bomb-examine"));
    }

    private void OnEvidenceBombVerbs(Entity<UplinkEvidenceBombComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !args.CanComplexInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("uplink-evidence-bomb-verb"),
            Priority = -10,
            Act = () => ArmEvidenceBomb(ent, user),
        });
    }

    private void ArmEvidenceBomb(Entity<UplinkEvidenceBombComponent> ent, EntityUid user)
    {
        if (ent.Comp.DetonateAt != null)
        {
            _popup.PopupEntity(Loc.GetString("uplink-evidence-bomb-already"), ent, user);
            return;
        }

        ent.Comp.DetonateAt = _timing.CurTime + ent.Comp.Delay;
        _appearance.SetData(ent, UplinkEvidenceBombVisuals.Exploding, true);
        _audio.PlayPvs(EvidenceBombArmSound, ent);
        _popup.PopupEntity(Loc.GetString("uplink-evidence-bomb-armed"), ent, user, PopupType.MediumCaution);
        _adminLog.Add(LogType.Explosion, LogImpact.Medium, $"{ToPrettyString(user):user} armed evidence disposal bomb {ToPrettyString(ent):entity}");
    }

    private void UpdateEvidenceBombs()
    {
        var query = EntityQueryEnumerator<UplinkEvidenceBombComponent>();
        while (query.MoveNext(out var uid, out var bomb))
        {
            if (bomb.DetonateAt is not { } at || _timing.CurTime < at)
                continue;

            bomb.DetonateAt = null;
            // explosion(light_impact_range = 2)
            _explosion.QueueExplosion(_transform.GetMapCoordinates(uid), "Default", 40f, 6f, 6f, uid, canCreateVacuum: false);
            QueueDel(uid);
        }
    }

    #endregion

    #region Силовой кулак

    private void OnFistExamined(Entity<PowerFistComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("uplink-powerfist-examine", ("pressure", ent.Comp.Pressure)));
    }

    /// <summary>wrench_act: переключение силы клапана.</summary>
    private void OnFistInteractUsing(Entity<PowerFistComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !_tool.HasQuality(args.Used, WrenchQuality))
            return;

        args.Handled = true;
        ent.Comp.Pressure = ent.Comp.Pressure >= 3 ? 1 : ent.Comp.Pressure + 1;
        Dirty(ent);
        _audio.PlayPvs(new SoundPathSpecifier("/Audio/Items/ratchet.ogg"), ent);
        _popup.PopupEntity(Loc.GetString("uplink-powerfist-pressure", ("pressure", ent.Comp.Pressure)), ent, args.User);
    }

    private void OnFistHit(Entity<PowerFistComponent> ent, ref MeleeHitEvent args)
    {
        if (!args.IsHit || args.HitEntities.Count == 0)
            return;

        EntityUid? target = null;
        foreach (var hit in args.HitEntities)
        {
            if (HasComp<MobStateComponent>(hit))
            {
                target = hit;
                break;
            }
        }

        if (target == null)
            return;

        var tankEnt = _itemSlots.GetItemOrNull(ent, ent.Comp.TankSlot);
        if (tankEnt == null || !TryComp<GasTankComponent>(tankEnt, out var tank))
        {
            _popup.PopupEntity(Loc.GetString("uplink-powerfist-no-tank"), ent, args.User);
            return;
        }

        var wanted = ent.Comp.GasPerFist * ent.Comp.Pressure;
        var used = _gasTank.RemoveAir((tankEnt.Value, tank), wanted);
        var moles = used.TotalMoles;

        if (_atmos.GetContainingMixture(ent.Owner, false, true) is { } environment)
            _atmos.Merge(environment, used);

        // Пустой баллон: удар на force / 5 (это и есть базовый урон кулака).
        if (moles <= 0.01f)
        {
            _popup.PopupEntity(Loc.GetString("uplink-powerfist-empty"), ent, args.User);
            return;
        }

        // Газа не хватило: force / 2.
        if (moles < wanted - 0.01f)
        {
            _popup.PopupEntity(Loc.GetString("uplink-powerfist-weak"), ent, args.User);
            args.BonusDamage += new DamageSpecifier(args.BaseDamage) * ((ent.Comp.Force / 2f) / ent.Comp.Force * 5f - 1f);
            args.HitSoundOverride = FistWeakSound;
            return;
        }

        // Полный удар: force * давление и отбрасывание на 5 * давление тайлов.
        args.BonusDamage += new DamageSpecifier(args.BaseDamage) * (ent.Comp.Pressure * 5f - 1f);
        args.HitSoundOverride = FistBlastSound;
        _popup.PopupEntity(Loc.GetString("uplink-powerfist-blast", ("target", target.Value)), target.Value, PopupType.LargeCaution);

        var direction = _transform.GetWorldPosition(target.Value) - _transform.GetWorldPosition(args.User);
        if (direction.LengthSquared() < 0.001f)
            direction = new Vector2(0, 1);

        _throwing.TryThrow(target.Value,
            Vector2.Normalize(direction) * (5f * ent.Comp.Pressure),
            5f + ent.Comp.Pressure * 2.5f,
            args.User,
            recoil: false);
    }

    #endregion
}
