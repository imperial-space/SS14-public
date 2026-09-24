using Content.Shared.Imperial.DeimonFly.DeathNote.UI;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.DeimonFly.DeathNote.UI;

public sealed class DeathNoteAdminBoundUserInterface : BoundUserInterface
{
    private DeathNoteAdminLedgerWindow? _window;

    public DeathNoteAdminBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DeathNoteAdminLedgerWindow>();
        _window.OnPageRequested += page =>
            SendMessage(new DeathNoteAdminPageRequestMessage(page));
        SendMessage(new DeathNoteAdminPageRequestMessage(0));
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        base.ReceiveMessage(message);
        if (message is DeathNoteAdminPageResponseMessage response)
            _window?.UpdatePage(response);
    }
}
