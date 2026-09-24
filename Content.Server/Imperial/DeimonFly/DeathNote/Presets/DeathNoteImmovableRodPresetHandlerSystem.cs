using System.Numerics;
using Content.Server.ImmovableRod;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.Gibbing;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Направляет штатный неостановимый стержень через координаты цели на момент исполнения.
/// Движение, столкновения, разрушение и побочный ущерб остаются штатными.
/// </summary>
public sealed class DeathNoteImmovableRodPresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly GunSystem _gun = default!;
    [Dependency] private readonly GibbingSystem _gibbing = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.ImmovableRod;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeathNoteHomingRodComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var homing, out var physics, out var xform))
        {
            if (Deleted(homing.Target) || _mobState.IsDead(homing.Target))
            {
                RemCompDeferred<DeathNoteHomingRodComponent>(uid);
                continue;
            }

            var rodCoordinates = _transform.GetMapCoordinates((uid, xform));
            var targetCoordinates = _transform.GetMapCoordinates(homing.Target);
            if (rodCoordinates.MapId == MapId.Nullspace ||
                targetCoordinates.MapId == MapId.Nullspace ||
                rodCoordinates.MapId != targetCoordinates.MapId)
            {
                RemCompDeferred<DeathNoteHomingRodComponent>(uid);
                continue;
            }

            var offset = targetCoordinates.Position - rodCoordinates.Position;
            if (offset.LengthSquared() <= 0.0001f)
                continue;

            var desiredDirection = Vector2.Normalize(offset);
            var currentDirection = physics.LinearVelocity.LengthSquared() > 0.0001f
                ? Vector2.Normalize(physics.LinearVelocity)
                : desiredDirection;
            var turn = Math.Clamp(frameTime * homing.TurnResponsiveness, 0f, 1f);
            var blended = Vector2.Lerp(currentDirection, desiredDirection, turn);
            var direction = blended.LengthSquared() > 0.0001f
                ? Vector2.Normalize(blended)
                : desiredDirection;
            var velocity = direction * homing.Speed;

            if (DistanceSquaredToSegment(
                    targetCoordinates.Position,
                    rodCoordinates.Position,
                    rodCoordinates.Position + velocity * frameTime) <= homing.ImpactRadius * homing.ImpactRadius)
            {
                ApplyGuaranteedTargetImpact(uid, homing.Target);
                RemCompDeferred<DeathNoteHomingRodComponent>(uid);
                continue;
            }

            _physics.SetLinearVelocity(uid, velocity, body: physics);
            _transform.SetLocalRotation(uid, direction.ToWorldAngle() + MathHelper.PiOver2, xform);
        }
    }

    private void ApplyGuaranteedTargetImpact(EntityUid rod, EntityUid target)
    {
        if (Deleted(target) || !TryComp(rod, out ImmovableRodComponent? immovableRod))
            return;

        if (immovableRod.ShouldGib && HasComp<BodyComponent>(target))
        {
            immovableRod.MobCount++;
            _gibbing.Gib(target);
            return;
        }

        if (immovableRod.Damage != null)
            _damageable.TryChangeDamage(target, immovableRod.Damage, ignoreResistances: true, origin: rod);
    }

    private static float DistanceSquaredToSegment(Vector2 point, Vector2 start, Vector2 end)
    {
        var segment = end - start;
        var lengthSquared = segment.LengthSquared();
        if (lengthSquared <= 0.0001f)
            return Vector2.DistanceSquared(point, start);

        var projection = Math.Clamp(Vector2.Dot(point - start, segment) / lengthSquared, 0f, 1f);
        return Vector2.DistanceSquared(point, start + segment * projection);
    }

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before rod execution.");

        if (parameters.EntityPrototype is not { } prototypeId ||
            !_prototypeManager.TryIndex(prototypeId, out EntityPrototype? prototype) ||
            !prototype.TryGetComponent<ImmovableRodComponent>(out _, EntityManager.ComponentFactory))
        {
            return DeathNotePresetExecutionResult.Failed("Configured rod prototype is missing or has no ImmovableRod component.");
        }

        if (parameters.SpawnDistance <= 0f ||
            parameters.LaunchSpeed <= 0f ||
            parameters.TurnResponsiveness <= 0f ||
            parameters.ImpactRadius <= 0f)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Rod movement parameters must be positive.");
        }

        var targetCoordinates = _transform.GetMapCoordinates(context.Target);
        if (targetCoordinates.MapId == MapId.Nullspace)
            return DeathNotePresetExecutionResult.Failed("Target was in nullspace at rod execution time.");

        var direction = _random.NextAngle().ToVec();
        var spawnCoordinates = targetCoordinates.Offset(-direction * parameters.SpawnDistance);
        var rod = Spawn(prototypeId, spawnCoordinates);
        var homing = EnsureComp<DeathNoteHomingRodComponent>(rod);
        homing.Target = context.Target;
        homing.Speed = parameters.LaunchSpeed;
        homing.TurnResponsiveness = parameters.TurnResponsiveness;
        homing.ImpactRadius = parameters.ImpactRadius;
        EntityUid? source = !Deleted(context.Writer)
            ? context.Writer
            : !Deleted(context.Notebook)
                ? context.Notebook
                : null;

        _gun.ShootProjectile(
            rod,
            direction,
            Vector2.Zero,
            source,
            source,
            parameters.LaunchSpeed);

        return DeathNotePresetExecutionResult.Succeeded(
            $"Immovable rod {rod} was launched and will continuously home in on the target.");
    }
}
