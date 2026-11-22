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

    public IEnumerable<DeathNotePresetPrototype> Presets =>
        _prototypeManager.EnumeratePrototypes<DeathNotePresetPrototype>().Where(preset => preset.Enabled);

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

    public bool HasHandler(DeathNotePresetHandlerType type)
    {
        var ev = new DeathNotePresetHandlerAvailabilityEvent(type);
        RaiseLocalEvent(ref ev);

        return ev.Available && !IsAmbiguous(type, ev.Ambiguous);
    }

    public bool TryExecute(
        DeathNotePresetHandlerType type,
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        out DeathNotePresetExecutionResult result)
    {
        var ev = new DeathNotePresetExecutionEvent(type, context, parameters);
        RaiseLocalEvent(ref ev);

        if (!ev.Handled || ev.Result == null || IsAmbiguous(type, ev.Ambiguous))
        {
            result = default;
            return false;
        }

        result = ev.Result.Value;
        return true;
    }

    public bool TryGetPreludeDuration(
        DeathNotePresetHandlerType type,
        DeathNotePresetParameters parameters,
        out TimeSpan duration)
    {
        var ev = new DeathNotePresetPreludeDurationEvent(type, parameters);
        RaiseLocalEvent(ref ev);

        duration = ev.Duration;
        return ev.Handled && !IsAmbiguous(type, ev.Ambiguous);
    }

    public bool TryBeginPrelude(
        DeathNotePresetHandlerType type,
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters,
        out DeathNotePresetExecutionResult result)
    {
        var ev = new DeathNotePresetPreludeExecutionEvent(type, context, parameters);
        RaiseLocalEvent(ref ev);

        if (!ev.Handled || ev.Result == null || IsAmbiguous(type, ev.Ambiguous))
        {
            result = default;
            return false;
        }

        result = ev.Result.Value;
        return true;
    }

    private bool IsAmbiguous(DeathNotePresetHandlerType type, bool ambiguous)
    {
        if (!ambiguous)
            return false;

        Log.Error($"Death Note handler '{type}' was registered more than once.");
        return true;
    }
}
