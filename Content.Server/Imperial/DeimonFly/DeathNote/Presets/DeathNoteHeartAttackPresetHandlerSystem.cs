using Content.Server.Popups;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Content.Shared.Popups;
using Robust.Shared.Random;
using Robust.Shared.Audio.Systems;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Минимальный обработчик сердечного приступа через штатный урон и MobState.
/// </summary>
public sealed class DeathNoteHeartAttackPresetHandlerSystem : EntitySystem,
    IDeathNotePresetHandler,
    IDeathNotePresetPreludeHandler
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.HeartAttack;

    public TimeSpan GetPreludeDuration(DeathNotePresetParameters parameters)
    {
        return parameters.PreludeDuration;
    }

    public DeathNotePresetExecutionResult BeginPrelude(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before heart attack prelude.");

        if (parameters.TargetPopup is { } popup)
        {
            _popup.PopupEntity(
                Loc.GetString(popup),
                context.Target,
                context.Target,
                PopupType.LargeCaution);
        }

        if (parameters.PreludeSound is { } sound)
            _audio.PlayEntity(sound, context.Target, context.Target);

        return DeathNotePresetExecutionResult.Succeeded("Heart attack warning popup and private sound were started.");
    }

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
    {
        if (Deleted(context.Target))
            return DeathNotePresetExecutionResult.Failed("Target entity was deleted before heart attack execution.");

        if (!DeathNoteDamageHelper.TryCreate(parameters, _random, out var damage))
            return DeathNotePresetExecutionResult.Failed("Heart attack damage parameters are invalid.");

        EntityUid? origin = Deleted(context.Writer) ? null : context.Writer;
        var changed = _damageable.TryChangeDamage(
            context.Target,
            damage,
            ignoreResistances: true,
            interruptsDoAfters: true,
            origin: origin,
            ignoreGlobalModifiers: true);

        return changed
            ? DeathNotePresetExecutionResult.Succeeded("Heart attack damage was applied through DamageableSystem.")
            : DeathNotePresetExecutionResult.Failed("Heart attack damage was blocked or the target is not damageable.");
    }
}
