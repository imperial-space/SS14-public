using System.Linq;
using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Parsing;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Ограниченный реестр явно объявленных прототипов и небольших серверных обработчиков.
/// </summary>
public sealed class DeathNotePresetRegistrySystem : EntitySystem, IDeathNotePresetRegistry
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntitySystemManager _systemManager = default!;

    private readonly Dictionary<DeathNotePresetHandlerType, IDeathNotePresetHandler> _handlers = new();

    public IEnumerable<DeathNotePresetPrototype> Presets =>
        _prototypeManager.EnumeratePrototypes<DeathNotePresetPrototype>().Where(preset => preset.Enabled);

    public override void Initialize()
    {
        base.Initialize();

        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteHeartAttackPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteImmovableRodPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteExplosionPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteFirePresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteAsphyxiationPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteElectrocutionPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNotePoisonPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteMeteorPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteFaunaPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteDirectDamagePresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteLightningPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteHostileFactionPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteGuidedAirlockPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteGuidedDisposalPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteGuidedVendingPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteGuidedFoodPoisoningPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteGuidedDrinkPoisoningPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteCeilingCollapsePresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteMimicPresetHandlerSystem>());
        RegisterHandler(_systemManager.GetEntitySystem<DeathNoteBluespaceAnomalyPresetHandlerSystem>());
    }

    public bool TryResolve(string cause, out DeathNotePresetPrototype? preset)
    {
        var normalized = DeathNoteInputParser.NormalizeCause(cause);
        DeathNotePresetPrototype? match = null;

        foreach (var candidate in Presets)
        {
            if (!string.Equals(candidate.ID, normalized, StringComparison.OrdinalIgnoreCase) &&
                !candidate.Aliases.Any(alias =>
                    string.Equals(
                        DeathNoteInputParser.NormalizeCause(alias),
                        normalized,
                        StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (match != null)
            {
                Log.Error($"Death Note preset alias '{normalized}' is ambiguous between '{match.ID}' and '{candidate.ID}'.");
                preset = null;
                return false;
            }

            match = candidate;
        }

        preset = match;
        return preset != null;
    }

    public bool TryGetHandler(DeathNotePresetHandlerType type, out IDeathNotePresetHandler? handler)
    {
        return _handlers.TryGetValue(type, out handler);
    }

    private void RegisterHandler(IDeathNotePresetHandler handler)
    {
        if (!_handlers.TryAdd(handler.HandlerType, handler))
            Log.Error($"Death Note handler '{handler.HandlerType}' was registered more than once.");
    }
}
