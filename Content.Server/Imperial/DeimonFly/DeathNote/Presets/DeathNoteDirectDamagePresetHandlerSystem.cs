using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Применяет один из штатных типов урона со случайным итогом из прототипа.
/// </summary>
public sealed class DeathNoteDirectDamagePresetHandlerSystem : DeathNotePresetHandlerSystem, IDeathNotePresetHandler
{
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.DirectDamage;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before direct damage execution.");

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Direct damage parameters are invalid.");

        EntityUid? origin = Deleted(context.Writer) ? null : context.Writer;
        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: origin,
            ignoreGlobalModifiers: true);

        return changed
            ? DeathNotePresetExecutionResult.Succeeded($"Applied randomized direct damage: {damage.GetTotal()}.")
            : DeathNotePresetExecutionResult.Failed("Direct damage was blocked or the target is not damageable.");
    }
}
