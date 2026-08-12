using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.DeimonFly.DeathNote.UI;

public sealed class DeathNoteBoundUserInterface : BoundUserInterface
{
    private DeathNoteWindow? _window;
    private DeathNoteBoundUserInterfaceState? _lastState;

    internal DeathNoteBoundUserInterfaceState? LastState => _lastState;

    public DeathNoteBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DeathNoteWindow>();
        _window.OnSubmit += (pageIndex, entryText, revision) =>
            SendMessage(new DeathNoteSubmitMessage(pageIndex, entryText, revision));
        _window.OnSpreadRequested += spreadIndex =>
            SendMessage(new DeathNoteSpreadRequestMessage(spreadIndex));
        if (_lastState != null)
            _window.UpdateState(_lastState);
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        switch (message)
        {
            case DeathNoteNotebookStateMessage state:
                _lastState = state.State;
                _window?.UpdateState(state.State);
                break;
            case DeathNoteSubmissionResponseMessage response:
                _window?.ShowFeedback(response);
                break;
        }
    }
}
