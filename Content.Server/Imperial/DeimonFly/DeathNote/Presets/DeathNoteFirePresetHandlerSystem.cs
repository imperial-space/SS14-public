using Content.Server.Atmos.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Поджигает цель через штатные fire stacks и FlammableSystem.
/// </summary>
public sealed class DeathNoteFirePresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly FlammableSystem _flammable = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Fire;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target) || !TryComp(context.Target, out FlammableComponent? flammable))
            return DeathNotePresetExecutionResult.Failed("Target was deleted or cannot be ignited.");

        if (parameters.FireStacks <= 0f)
            return DeathNotePresetExecutionResult.Failed("Configured fire stacks must be positive.");

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Fire damage parameters are invalid.");

        var source = !Deleted(context.Notebook) ? context.Notebook : context.Target;
        EntityUid? writer = !Deleted(context.Writer) ? context.Writer : null;
        _flammable.AdjustFireStacks(context.Target, parameters.FireStacks, flammable);
        _flammable.Ignite(context.Target, source, flammable, writer);

        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: writer,
            ignoreGlobalModifiers: true);

        if (!flammable.OnFire && !changed)
            return DeathNotePresetExecutionResult.Failed("Target could not be ignited or damaged.");

        return DeathNotePresetExecutionResult.Succeeded(flammable.OnFire && changed
            ? "Target was ignited and received guaranteed burn damage."
            : "At least one configured fire effect was applied to the target.");
    }
}
