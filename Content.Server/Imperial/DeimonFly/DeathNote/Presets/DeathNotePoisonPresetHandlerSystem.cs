using Content.Server.Body.Systems;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Вводит настроенный штатный реагент в кровоток, оставляя эффект метаболизму.
/// </summary>
public sealed class DeathNotePoisonPresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly BloodstreamSystem _bloodstream = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.Poison;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before poison execution.");

        if (parameters.Reagent is not { } reagent ||
            parameters.ReagentQuantity <= FixedPoint2.Zero ||
            !_prototypeManager.HasIndex<ReagentPrototype>(reagent))
        {
            return DeathNotePresetExecutionResult.Failed("Configured poison reagent or quantity is invalid.");
        }

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Poison damage parameters are invalid.");

        var solution = new Solution(reagent, parameters.ReagentQuantity);
        if (!_bloodstream.TryAddToBloodstream(context.Target, solution))
            return DeathNotePresetExecutionResult.Failed(
                "Configured poison reagent could not be added to the target bloodstream.");

        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: Deleted(context.Writer) ? null : context.Writer,
            ignoreGlobalModifiers: true);

        return DeathNotePresetExecutionResult.Succeeded(changed
            ? "Configured poison reagent and guaranteed poison damage were applied."
            : "Configured poison reagent was added; its additional direct damage could not be applied.");
    }
}
