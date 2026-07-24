using System.Numerics;
using Content.Server.Imperial.DeimonFly.DeathNote.Components;
using Content.Server.Mining;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Mobs.Systems;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Запускает штатный метеор через координаты цели на момент исполнения.
/// </summary>
public sealed class DeathNoteMeteorPresetHandlerSystem : EntitySystem, IDeathNotePresetHandler
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Meteor;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<DeathNoteHomingMeteorComponent, PhysicsComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var homing, out var physics, out var xform))
        {
            if (Deleted(homing.Target) || _mobState.IsDead(homing.Target))
            {
                RemCompDeferred<DeathNoteHomingMeteorComponent>(uid);
                continue;
            }

            var meteorCoordinates = _transform.GetMapCoordinates((uid, xform));
            var targetCoordinates = _transform.GetMapCoordinates(homing.Target);
            if (meteorCoordinates.MapId == MapId.Nullspace ||
                meteorCoordinates.MapId != targetCoordinates.MapId)
            {
                RemCompDeferred<DeathNoteHomingMeteorComponent>(uid);
                continue;
            }

            var offset = targetCoordinates.Position - meteorCoordinates.Position;
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
                    meteorCoordinates.Position,
                    meteorCoordinates.Position + velocity * frameTime) <= homing.ImpactRadius * homing.ImpactRadius)
            {
                _damageable.TryChangeDamage(
                    homing.Target,
                    new DamageSpecifier(homing.Damage),
                    ignoreResistances: true,
                    interruptsDoAfters: true,
                    origin: uid,
                    ignoreGlobalModifiers: true);
                RemCompDeferred<DeathNoteHomingMeteorComponent>(uid);
                continue;
            }

            _physics.SetLinearVelocity(uid, velocity, body: physics);
            _transform.SetLocalRotation(uid, direction.ToWorldAngle() + MathHelper.PiOver2, xform);
        }
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
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before meteor execution.");

        if (parameters.EntityPrototype is not { } prototypeId ||
            !_prototypeManager.TryIndex(prototypeId, out EntityPrototype? prototype) ||
            !prototype.TryGetComponent<MeteorComponent>(out _, EntityManager.ComponentFactory))
        {
            return DeathNotePresetExecutionResult.Failed("Configured meteor prototype is missing or has no Meteor component.");
        }

        if (parameters.SpawnDistance <= 0f ||
            parameters.LaunchSpeed <= 0f ||
            parameters.TurnResponsiveness <= 0f ||
            parameters.ImpactRadius <= 0f)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Meteor movement parameters must be positive.");
        }

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Meteor damage parameters are invalid.");

        var targetCoordinates = _transform.GetMapCoordinates(context.Target);
        if (targetCoordinates.MapId == MapId.Nullspace)
            return DeathNotePresetExecutionResult.Failed("Target was in nullspace at meteor execution time.");

        var direction = _random.NextAngle().ToVec();
        var spawnCoordinates = targetCoordinates.Offset(-direction * parameters.SpawnDistance);
        var meteor = Spawn(prototypeId, spawnCoordinates);
        if (!TryComp(meteor, out PhysicsComponent? physics))
        {
            QueueDel(meteor);
            return DeathNotePresetExecutionResult.Failed("Configured meteor prototype has no Physics component.");
        }

        _physics.ApplyLinearImpulse(
            meteor,
            direction * parameters.LaunchSpeed * physics.Mass,
            body: physics);

        var homing = EnsureComp<DeathNoteHomingMeteorComponent>(meteor);
        homing.Target = context.Target;
        homing.Speed = parameters.LaunchSpeed;
        homing.TurnResponsiveness = parameters.TurnResponsiveness;
        homing.ImpactRadius = parameters.ImpactRadius;
        homing.Damage = damage;

        return DeathNotePresetExecutionResult.Succeeded(
            $"Meteor {meteor} was launched and will continuously home in on the target.");
    }
}
