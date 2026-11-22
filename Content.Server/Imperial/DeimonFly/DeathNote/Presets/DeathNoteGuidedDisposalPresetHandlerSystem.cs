using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

public sealed class DeathNoteGuidedDisposalPresetHandlerSystem : DeathNotePresetHandlerSystem,
    IDeathNotePresetHandler,
    IDeathNotePresetPreludeHandler
{
    [Dependency] private readonly DeathNoteGuidanceSystem _guidance = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.GuidedDisposal;

    public TimeSpan GetPreludeDuration(DeathNotePresetParameters parameters) => parameters.PreludeDuration;

    public DeathNotePresetExecutionResult BeginPrelude(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
        => _guidance.Begin(context, parameters, DeathNoteGuidedScenarioType.DisposalCatastrophe);

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
        => _guidance.Finish(context, DeathNoteGuidedScenarioType.DisposalCatastrophe);
}
