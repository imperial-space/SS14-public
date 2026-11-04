using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

// ─── GulagTeleporterConsole ────────────────────────────────────────────────

public sealed class GulagConsoleBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;
    private readonly GoalField _goal = new();

    public GulagConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _goal.Submitted += goal => SendMessage(new GulagConsoleSetGoalMessage(goal));
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("gulag-console-title"), new Vector2(420, 460), out _body);
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not GulagConsoleState s || _body == null)
            return;

        var focused = SecurityUi.FocusedEdit(new[] { _goal.Edit });
        var cursor = focused?.CursorPosition ?? 0;
        _body.RemoveAllChildren();
        var none = Loc.GetString("gulag-console-none");

        _body.AddChild(SecurityUi.Header(Loc.GetString("gulag-console-teleporter")));
        if (s.HasTeleporter)
        {
            _body.AddChild(SecurityUi.Field(Loc.GetString("gulag-console-location"), s.TeleporterLocation ?? none));
            var row = SecurityUi.Row();
            row.AddChild(SecurityUi.Button(Loc.GetString(s.TeleporterOpen ? "gulag-console-close" : "gulag-console-open"),
                () => Send(GulagConsoleAction.ToggleOpen)));
            row.AddChild(SecurityUi.Button(Loc.GetString(s.TeleporterLocked ? "gulag-console-unlock" : "gulag-console-lock"),
                () => Send(GulagConsoleAction.ToggleLock)));
            _body.AddChild(row);
        }
        else
        {
            _body.AddChild(new Label { Text = Loc.GetString("gulag-console-no-teleporter") });
        }
        _body.AddChild(SecurityUi.Button(Loc.GetString("gulag-console-scan"), () => Send(GulagConsoleAction.ScanTeleporter)));

        _body.AddChild(SecurityUi.Header(Loc.GetString("gulag-console-beacon")));
        _body.AddChild(SecurityUi.Field(Loc.GetString("gulag-console-location"),
            s.HasBeacon ? s.BeaconLocation ?? none : Loc.GetString("gulag-console-no-beacon")));
        _body.AddChild(SecurityUi.Button(Loc.GetString("gulag-console-scan"), () => Send(GulagConsoleAction.ScanBeacon)));

        _body.AddChild(SecurityUi.Header(Loc.GetString("gulag-console-prisoner")));
        _body.AddChild(SecurityUi.Field(Loc.GetString("gulag-console-name"), s.PrisonerName ?? none));
        _body.AddChild(SecurityUi.Field(Loc.GetString("gulag-console-crimstat"), s.CrimeStatus ?? none));

        _body.AddChild(SecurityUi.Header(Loc.GetString("gulag-console-id")));
        _body.AddChild(SecurityUi.Button(s.HasId ? s.IdName ?? none : Loc.GetString("gulag-console-insert-id"),
            () => Send(GulagConsoleAction.HandleId)));
        if (s.HasId)
        {
            _goal.SetServerGoal(s.Goal);
            _body.AddChild(_goal.BuildRow());
        }

        _body.AddChild(SecurityUi.Button(Loc.GetString("gulag-console-teleport"), () => Send(GulagConsoleAction.Teleport),
            !s.CanTeleport));
        SecurityUi.RestoreFocus(focused, cursor);
    }

    private void Send(GulagConsoleAction action)
    {
        SendMessage(new GulagConsoleActionMessage(action));
    }
}
