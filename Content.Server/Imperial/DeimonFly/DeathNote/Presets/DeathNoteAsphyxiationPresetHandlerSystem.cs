using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Применяет настроенный урон удушьем через DamageableSystem.
/// </summary>
public sealed class DeathNoteAsphyxiationPresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Asphyxiation;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before asphyxiation execution.");

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Asphyxiation damage parameters are invalid.");

        EntityUid? origin = !Deleted(context.Writer) ? context.Writer : null;
        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: origin,
            ignoreGlobalModifiers: true);

        return changed
            ? DeathNotePresetExecutionResult.Succeeded("Asphyxiation damage was applied through DamageableSystem.")
            : DeathNotePresetExecutionResult.Failed("Asphyxiation damage was blocked or the target is not damageable.");
    }
}
