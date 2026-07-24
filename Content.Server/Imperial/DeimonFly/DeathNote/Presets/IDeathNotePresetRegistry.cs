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

    bool TryGetHandler(DeathNotePresetHandlerType type, out IDeathNotePresetHandler? handler);
}
