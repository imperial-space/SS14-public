using Content.Shared.Imperial.DeimonFly.DeathNote;
using Content.Shared.Imperial.DeimonFly.DeathNote.Prototypes;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Presets;

/// <summary>
/// Вызывает методы систем-обработчиков через локальные события.
/// </summary>
public abstract class DeathNotePresetHandlerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DeathNotePresetHandlerAvailabilityEvent>(OnAvailabilityRequested);
        SubscribeLocalEvent<DeathNotePresetExecutionEvent>(OnExecutionRequested);
        SubscribeLocalEvent<DeathNotePresetPreludeDurationEvent>(OnPreludeDurationRequested);
        SubscribeLocalEvent<DeathNotePresetPreludeExecutionEvent>(OnPreludeExecutionRequested);
    }

    private void OnAvailabilityRequested(ref DeathNotePresetHandlerAvailabilityEvent args)
    {
        if (!TryGetHandler(args.HandlerType, out _))
            return;

        if (args.Available)
        {
            args.Ambiguous = true;
            return;
        }

        args.Available = true;
    }

    private void OnExecutionRequested(ref DeathNotePresetExecutionEvent args)
    {
        if (!TryGetHandler(args.HandlerType, out var handler))
            return;

        if (args.Handled)
        {
            args.Ambiguous = true;
            return;
        }

        args.Handled = true;
        args.Result = handler.Execute(args.Context, args.Parameters);
    }

    private void OnPreludeDurationRequested(ref DeathNotePresetPreludeDurationEvent args)
    {
        if (!TryGetHandler(args.HandlerType, out var handler) ||
            handler is not IDeathNotePresetPreludeHandler preludeHandler)
        {
            return;
        }

        if (args.Handled)
        {
            args.Ambiguous = true;
            return;
        }

        args.Handled = true;
        args.Duration = preludeHandler.GetPreludeDuration(args.Parameters);
    }

    private void OnPreludeExecutionRequested(ref DeathNotePresetPreludeExecutionEvent args)
    {
        if (!TryGetHandler(args.HandlerType, out var handler) ||
            handler is not IDeathNotePresetPreludeHandler preludeHandler)
        {
            return;
        }

        if (args.Handled)
        {
            args.Ambiguous = true;
            return;
        }

        args.Handled = true;
        args.Result = preludeHandler.BeginPrelude(args.Context, args.Parameters);
    }

    private bool TryGetHandler(
        DeathNotePresetHandlerType handlerType,
        out IDeathNotePresetHandler handler)
    {
        if (this is IDeathNotePresetHandler presetHandler && presetHandler.HandlerType == handlerType)
        {
            handler = presetHandler;
            return true;
        }

        handler = default!;
        return false;
    }
}

[ByRefEvent]
public record struct DeathNotePresetHandlerAvailabilityEvent(DeathNotePresetHandlerType HandlerType)
{
    public bool Available;
    public bool Ambiguous;
}

[ByRefEvent]
public record struct DeathNotePresetExecutionEvent(
    DeathNotePresetHandlerType HandlerType,
    DeathNotePresetExecutionContext Context,
    DeathNotePresetParameters Parameters)
{
    public bool Handled;
    public bool Ambiguous;
    public DeathNotePresetExecutionResult? Result;
}

[ByRefEvent]
public record struct DeathNotePresetPreludeDurationEvent(
    DeathNotePresetHandlerType HandlerType,
    DeathNotePresetParameters Parameters)
{
    public bool Handled;
    public bool Ambiguous;
    public TimeSpan Duration;
}

[ByRefEvent]
public record struct DeathNotePresetPreludeExecutionEvent(
    DeathNotePresetHandlerType HandlerType,
    DeathNotePresetExecutionContext Context,
    DeathNotePresetParameters Parameters)
{
    public bool Handled;
    public bool Ambiguous;
    public DeathNotePresetExecutionResult? Result;
}
