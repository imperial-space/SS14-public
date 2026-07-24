using Content.Server.Electrocution;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Использует штатный урон и статус поражения электричеством.
/// </summary>
public sealed class DeathNoteElectrocutionPresetHandlerSystem : EntitySystem, IDeathNotePresetHandler
{
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Electrocution;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before electrocution execution.");

        if (parameters.ShockDamage <= 0 || parameters.EffectDuration <= TimeSpan.Zero)
            return DeathNotePresetExecutionResult.Failed("Electrocution damage and duration must be positive.");

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Electrocution damage parameters are invalid.");

        EntityUid? source = !Deleted(context.Notebook) ? context.Notebook : null;
        var success = _electrocution.TryDoElectrocution(
            context.Target,
            source,
            parameters.ShockDamage,
            parameters.EffectDuration,
            refresh: true,
            ignoreInsulation: parameters.IgnoreInsulation);

        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: source,
            ignoreGlobalModifiers: true);

        if (!success && !changed)
            return DeathNotePresetExecutionResult.Failed("Electrocution and its configured damage could not be applied.");

        return DeathNotePresetExecutionResult.Succeeded(success && changed
            ? "Target was shocked and received guaranteed electrical damage."
            : "At least one configured electrical effect was applied to the target.");
    }
}
