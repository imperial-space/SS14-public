using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

public sealed class DeathNoteGuidedAirlockPresetHandlerSystem : EntitySystem,
    IDeathNotePresetHandler
{
    [Dependency] private readonly DeathNoteGuidanceSystem _guidance = default!;

    public DeathNotePresetHandlerType HandlerType => DeathNotePresetHandlerType.GuidedAirlock;

    public DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters)
        => _guidance.BeginPersistentSilent(
            context,
            parameters,
            DeathNoteGuidedScenarioType.AirlockAccident);
}
