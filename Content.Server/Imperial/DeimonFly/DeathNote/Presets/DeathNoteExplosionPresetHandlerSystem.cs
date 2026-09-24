using Content.Server.Explosion.EntitySystems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Damage.Systems;
using Robust.Shared.Map;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Ставит в штатную очередь взрывов локальный взрыв на текущих координатах цели.
/// </summary>
public sealed class DeathNoteExplosionPresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly ExplosionSystem _explosion = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Explosion;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before explosion execution.");

        if (parameters.ExplosionType is not { } explosionType ||
            parameters.TotalIntensity <= 0f ||
            parameters.IntensitySlope <= 0f ||
            parameters.MaxTileIntensity <= 0f)
        {
            return DeathNotePresetExecutionResult.Failed("Explosion parameters are incomplete or invalid.");
        }

        var coordinates = _transform.GetMapCoordinates(context.Target);
        if (coordinates.MapId == MapId.Nullspace)
            return DeathNotePresetExecutionResult.Failed("Target was in nullspace at explosion execution time.");

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Explosion target damage parameters are invalid.");

        EntityUid? cause = !Deleted(context.Writer) ? context.Writer : null;
        _explosion.QueueExplosion(
            coordinates,
            explosionType,
            parameters.TotalIntensity,
            parameters.IntensitySlope,
            parameters.MaxTileIntensity,
            cause,
            parameters.TileBreakScale,
            parameters.MaxTileBreak,
            parameters.CanCreateVacuum);

        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: cause,
            ignoreGlobalModifiers: true);

        return DeathNotePresetExecutionResult.Succeeded(changed
            ? "A configured explosion was queued and guaranteed blast damage was applied to its target."
            : "A configured explosion was queued; its additional direct target damage could not be applied.");
    }
}
