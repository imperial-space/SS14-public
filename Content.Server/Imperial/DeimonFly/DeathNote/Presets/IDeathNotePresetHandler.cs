using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Независимый обработчик одного типа существующей игровой механики.
/// </summary>
public interface IDeathNotePresetHandler
{
    DeathNotePresetHandlerType HandlerType { get; }

    DeathNotePresetExecutionResult Execute(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters);
}

/// <summary>
/// Необязательная фаза, запускаемая до точного момента исполнения.
/// Её длительность задаётся прототипом, поэтому назначенное в тетради время остаётся точным.
/// </summary>
public interface IDeathNotePresetPreludeHandler
{
    TimeSpan GetPreludeDuration(DeathNotePresetParameters parameters);

    DeathNotePresetExecutionResult BeginPrelude(
        in DeathNotePresetExecutionContext context,
        DeathNotePresetParameters parameters);
}
