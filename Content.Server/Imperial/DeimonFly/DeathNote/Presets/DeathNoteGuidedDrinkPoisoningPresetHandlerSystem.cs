using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Запускает личное для жертвы окно отравления напитком.
/// Первые подходящие напитки, которых она коснётся, получают настоящий сохраняющийся яд.
/// </summary>
public sealed class DeathNoteGuidedDrinkPoisoningPresetHandlerSystem : EntitySystem,
    IDeathNotePresetHandler,
    IDeathNotePresetPreludeHandler
{
    [Dependency] private readonly DeathNoteGuidanceSystem _guidance = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.GuidedDrinkPoisoning;

    public TimeSpan GetPreludeDuration(DeathNotePresetParameters parameters) => parameters.PreludeDuration;

    public DeathNotePresetExecutionResult BeginPrelude(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
        => _guidance.Begin(context, parameters, DeathNoteGuidedScenarioType.PoisonedDrink);

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
        => _guidance.Finish(context, DeathNoteGuidedScenarioType.PoisonedDrink);
}
