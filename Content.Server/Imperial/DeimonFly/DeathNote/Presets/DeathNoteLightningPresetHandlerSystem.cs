using System.Numerics;
using Content.Server.Lightning;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Создаёт настоящую дугу молнии и наносит настроенный случайный электрический урон.
/// </summary>
public sealed class DeathNoteLightningPresetHandlerSystem : EntitySystem, IDeathNotePresetHandler
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly LightningSystem _lightning = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Lightning;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target) ||
            parameters.EntityPrototype is not { } sourcePrototype ||
            !_prototypes.HasIndex<EntityPrototype>(sourcePrototype) ||
            parameters.SpawnDistance <= 0f)
        {
            return DeathNotePresetExecutionResult.Failed(
                "Target entity or lightning source prototype is unavailable.");
        }

        var targetCoordinates = _transform.GetMapCoordinates(context.Target);
        if (targetCoordinates.MapId == MapId.Nullspace ||
            !DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
        {
            return DeathNotePresetExecutionResult.Failed(
                "Lightning target coordinates or damage parameters are invalid.");
        }

        var source = Spawn(
            sourcePrototype,
            Transform(context.Target).Coordinates.Offset(new Vector2(0f, parameters.SpawnDistance)));
        _lightning.ShootLightning(source, context.Target);

        EntityUid? origin = Deleted(context.Writer) ? null : context.Writer;
        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: origin,
            ignoreGlobalModifiers: true);

        return changed
            ? DeathNotePresetExecutionResult.Succeeded($"A lightning beam struck the target for {damage.GetTotal()} configured damage.")
            : DeathNotePresetExecutionResult.Failed("Lightning damage was blocked or the target is not damageable.");
    }
}
