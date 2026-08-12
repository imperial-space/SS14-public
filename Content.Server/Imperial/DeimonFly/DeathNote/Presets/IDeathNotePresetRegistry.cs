using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Контракт ограниченного реестра причин и их обработчиков.
/// </summary>
public interface IDeathNotePresetRegistry
{
    IEnumerable<DeathNotePresetPrototype> Presets { get; }

    bool TryResolve(string cause, out DeathNotePresetPrototype? preset);

    bool HasHandler(DeathNotePresetHandlerType type);

    bool TryExecute(
        DeathNotePresetHandlerType type,
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        out DeathNotePresetExecutionResult result);

    bool TryGetPreludeDuration(
        DeathNotePresetHandlerType type,
        DeathNotePresetParameters parameters,
        out TimeSpan duration);

    bool TryBeginPrelude(
        DeathNotePresetHandlerType type,
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        out DeathNotePresetExecutionResult result);
}
