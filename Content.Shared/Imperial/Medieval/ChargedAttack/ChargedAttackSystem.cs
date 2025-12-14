using System.Linq;
using System.Numerics;
using Content.Shared.Damage.Events;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee;
using Content.Shared.Weapons.Melee.Events;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.Medieval.ChargedAttack;

public sealed class ChargedAttackSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedMeleeWeaponSystem _weaponSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transformSystem = default!;
    [Dependency] private readonly SharedPhysicsSystem _physicsSystem = default!;
    [Dependency] private readonly SharedStaminaSystem _staminaSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _modifierSystem = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<ChargedAttackStart>(OnStart);
        SubscribeAllEvent<ChargedAttackEnd>(OnEnd);

        SubscribeLocalEvent<HumanoidAppearanceComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshMovespeed);

        SubscribeLocalEvent<ChargedAttackComponent, MeleeHitEvent>(OnHit);
        SubscribeLocalEvent<ChargedAttackComponent, StaminaMeleeHitEvent>(OnStaminaHit);
    }

    private void OnHit(EntityUid weapon, ChargedAttackComponent charged, MeleeHitEvent args)
    {
        if (!args.HitEntities.Any())
            return;

        if (charged.Modifier == 0f)
            return;

        var modifier = charged.Modifier - 1;

        args.BonusDamage = args.BaseDamage * modifier;
        charged.Modifier = 0f;
        Dirty(weapon, charged);
    }

    private void OnStaminaHit(EntityUid weapon, ChargedAttackComponent charged, StaminaMeleeHitEvent args)
    {
        if (!args.HitList.Any())
            return;

        if (charged.Modifier == 0f)
            return;

        args.Multiplier *= charged.Modifier;
        charged.Modifier = 0f;
        Dirty(weapon, charged);
    }

    public override void Update(float frameTime)
    {
        var query = EntityQueryEnumerator<ChargedAttackComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.EffectSpawnedEntity.IsValid())
                continue;

            if (comp.AttackStart + TimeSpan.FromSeconds(comp.MaxAttackTime) <= _timing.CurTime || !comp.CurrentAttacking)
            {
                EndEffect(uid, comp);
                continue;
            }

            var effect = comp.EffectSpawnedEntity;
            var parent = Transform(effect).ParentUid;
            if (!_handsSystem.TryGetActiveItem(parent, out var item) || item != uid || _mobState.IsCritical(parent) || _mobState.IsDead(parent))
            {
                EndEffect(uid, comp);
                continue;
            }
        }
    }

    private void OnRefreshMovespeed(EntityUid uid, HumanoidAppearanceComponent component, RefreshMovementSpeedModifiersEvent args)
    {
        var item = _handsSystem.EnumerateHeld(uid).Where(c => HasComp<ChargedAttackComponent>(c)).FirstOrNull();

        if (item == null || !TryComp<ChargedAttackComponent>(item, out var comp))
            return;

        if (comp.CurrentAttacking)
        {
            args.ModifySpeed(comp.SpeedModifer, comp.SpeedModifer);
        }
    }

    private void OnStart(ChargedAttackStart msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var weaponUid = GetEntity(msg.Weapon);
        if (!TryComp<ChargedAttackComponent>(weaponUid, out var charged)) return;
        charged.AttackStart = _timing.CurTime;
        charged.CurrentAttacking = true;
        Dirty(weaponUid, charged);
        _modifierSystem.RefreshMovementSpeedModifiers(user);
        StartEffect(weaponUid, charged, user);
    }

    private void OnEnd(ChargedAttackEnd msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } user)
            return;

        var coordinates = GetCoordinates(msg.Coordinates);
        var weaponUid = GetEntity(msg.Weapon);
        var attackTime = msg.AttackTime;

        if (!TryComp<ChargedAttackComponent>(weaponUid, out var charged)) return;
        if (!TryComp<MeleeWeaponComponent>(weaponUid, out var weapon)) return;
        ChargedAttack(user, coordinates, (weaponUid, weapon), charged, attackTime, args.SenderSession);
        StopAttacking(weaponUid, charged, user);
    }

    private void ChargedAttack(EntityUid entity, EntityCoordinates coordinates, Entity<MeleeWeaponComponent> weapon, ChargedAttackComponent charged, TimeSpan attackTime, ICommonSession session)
    {
        var ratio = Math.Min(attackTime.Seconds / charged.MaxAttackTime, 1f);
        charged.Modifier = charged.StaticModifier * ratio + 1f;
        Dirty(weapon.Owner, charged);

        if (!_staminaSystem.TryTakeStamina(entity, charged.StaminaDamage))
            return;

        _weaponSystem.AttemptHeavyAttack(entity, weapon.Owner, weapon.Comp, coordinates);

        // var direction = _transformSystem.ToMapCoordinates(coordinates).Position
        //       - _transformSystem.GetWorldPosition(entity);

        // if (direction == Vector2.Zero)
        //     return;

        // Dash(entity, charged, direction);
    }

    // private void Dash(EntityUid entity, ChargedAttackComponent charged, Vector2 direction)
    // {
    //     if (!TryComp<PhysicsComponent>(entity, out var physics))
    //         return;

    //     var impulseVector = direction.Normalized() * charged.VectorLenght * physics.Mass;
    //     _physicsSystem.SetLinearVelocity(entity, impulseVector, body: physics);
    // }

    public void StopAttacking(EntityUid entity, ChargedAttackComponent charged, EntityUid user)
    {
        charged.CurrentAttacking = false;
        charged.AttackStart = TimeSpan.FromSeconds(0f);
        _modifierSystem.RefreshMovementSpeedModifiers(user);
        EndEffect(entity, charged);
        Dirty(entity, charged);
    }

    private void StartEffect(EntityUid entity, ChargedAttackComponent charged, EntityUid user)
    {
        var effectProtoId = charged.EffectProtoId;
        var userXform = Transform(user);
        var effect = PredictedSpawnAttachedTo(effectProtoId, userXform.Coordinates);
        _transformSystem.SetLocalRotation(effect, userXform.LocalRotation);
        _transformSystem.SetParent(effect, user);
        charged.EffectSpawnedEntity = effect;
        Dirty(entity, charged);
    }

    private void EndEffect(EntityUid entity, ChargedAttackComponent charged)
    {
        var effect = charged.EffectSpawnedEntity;
        if (!effect.Valid)
            return;

        PredictedQueueDel(effect);
        charged.EffectSpawnedEntity = EntityUid.Invalid;
        Dirty(entity, charged);
    }
}
