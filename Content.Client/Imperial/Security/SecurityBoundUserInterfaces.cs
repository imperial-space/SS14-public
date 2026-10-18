using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

/// <summary>Общие кирпичики для окон консолей СБ.</summary>
internal static class SecurityUi
{
    public static BoxContainer Column(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = separation };
    }

    public static BoxContainer Row(int separation = 4)
    {
        return new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = separation };
    }

    public static Label Header(string text)
    {
        return new Label { Text = text, StyleClasses = { "LabelHeading" } };
    }

    public static BoxContainer Field(string name, string value)
    {
        var row = Row();
        row.AddChild(new Label { Text = name + ":", MinWidth = 140 });
        row.AddChild(new Label { Text = value, HorizontalExpand = true, ClipText = true });
        return row;
    }

    public static Button Button(string text, Action onPressed, bool disabled = false)
    {
        var button = new Button { Text = text, Disabled = disabled };
        button.OnPressed += _ => onPressed();
        return button;
    }

    /// <summary>
    /// Окно перестраивается раз в секунду; чтобы набранный текст и фокус не пропадали,
    /// поля ввода переиспользуются, а фокус возвращается после перестройки.
    /// </summary>
    public static LineEdit? FocusedEdit(IEnumerable<LineEdit> edits)
    {
        foreach (var edit in edits)
        {
            if (edit.HasKeyboardFocus())
                return edit;
        }

        return null;
    }

    public static void RestoreFocus(LineEdit? edit, int cursor)
    {
        if (edit == null)
            return;

        edit.GrabKeyboardFocus();
        edit.CursorPosition = Math.Min(cursor, edit.Text.Length);
    }

    public static DefaultWindow Window(string title, Vector2 size, out BoxContainer body)
    {
        var window = new DefaultWindow { Title = title, MinSize = size, SetSize = size };
        body = Column(6);
        window.Contents.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { body } });
        return window;
    }
}

/// <summary>Поле цели в очках с кнопкой «Задать».</summary>
internal sealed class GoalField
{
    public readonly LineEdit Edit = new() { HorizontalExpand = true };
    private readonly Button _set = new() { Text = Loc.GetString("gulag-console-set-goal") };
    private int? _lastServerGoal;

    public event Action<int>? Submitted;

    public GoalField()
    {
        Edit.OnTextEntered += _ => Submit();
        _set.OnPressed += _ => Submit();
    }

    /// <summary>Значение с сервера пишется в поле только если оно изменилось и поле не редактируется.</summary>
    public void SetServerGoal(int goal)
    {
        if (_lastServerGoal == goal)
            return;

        _lastServerGoal = goal;
        if (!Edit.HasKeyboardFocus())
            Edit.Text = goal.ToString();
    }

    public BoxContainer BuildRow()
    {
        (Edit.Parent as Control)?.RemoveChild(Edit);
        (_set.Parent as Control)?.RemoveChild(_set);

        var row = SecurityUi.Row();
        row.AddChild(new Label { Text = Loc.GetString("gulag-console-goal") + ":", MinWidth = 140 });
        row.AddChild(Edit);
        row.AddChild(_set);
        return row;
    }

    private void Submit()
    {
        if (int.TryParse(Edit.Text.Trim(), out var goal))
            Submitted?.Invoke(goal);
    }
}

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

// ─── PrisonerManagement ────────────────────────────────────────────────────

public sealed class PrisonerManagementBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    private readonly GoalField _goal = new();

    /// <summary>Поля сообщений по имплантам — переживают перестройку окна.</summary>
    private readonly Dictionary<NetEntity, LineEdit> _warnEdits = new();

    public PrisonerManagementBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        _goal.Submitted += goal => SendMessage(new PrisonerManagementSetGoalMessage(goal));
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("prisoner-management-title"), new Vector2(520, 560), out _body);
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
        if (state is not PrisonerManagementState s || _body == null)
            return;

        var focused = SecurityUi.FocusedEdit(_warnEdits.Values.Append(_goal.Edit));
        var cursor = focused?.CursorPosition ?? 0;
        _body.RemoveAllChildren();

        _body.AddChild(SecurityUi.Header(Loc.GetString("prisoner-management-id")));
        if (s.HasId)
        {
            _body.AddChild(SecurityUi.Field(Loc.GetString("gulag-console-name"), s.IdName ?? string.Empty));
            _body.AddChild(SecurityUi.Field(Loc.GetString("prisoner-management-points"), $"{s.Points} / {s.Goal}"));

            _goal.SetServerGoal(s.Goal);
            _body.AddChild(_goal.BuildRow());

            var row = SecurityUi.Row();
            row.AddChild(SecurityUi.Button(Loc.GetString("prisoner-management-reset"),
                () => SendMessage(new PrisonerManagementActionMessage(PrisonerManagementAction.ResetId))));
            row.AddChild(SecurityUi.Button(Loc.GetString("prisoner-management-eject"),
                () => SendMessage(new PrisonerManagementActionMessage(PrisonerManagementAction.EjectId))));
            _body.AddChild(row);
        }
        else
        {
            _body.AddChild(SecurityUi.Button(Loc.GetString("gulag-console-insert-id"),
                () => SendMessage(new PrisonerManagementActionMessage(PrisonerManagementAction.InsertId))));
        }

        _body.AddChild(SecurityUi.Header(Loc.GetString("prisoner-management-implants")));
        if (s.Implants.Count == 0)
            _body.AddChild(new Label { Text = Loc.GetString("prisoner-management-no-implants") });

        foreach (var implant in s.Implants)
        {
            var box = SecurityUi.Column(2);
            box.AddChild(new Label { Text = implant.Category, StyleClasses = { "LabelSubText" } });
            for (var i = 0; i < implant.InfoKeys.Count; i++)
            {
                box.AddChild(SecurityUi.Field(implant.InfoKeys[i], implant.InfoValues[i]));
            }

            var buttons = SecurityUi.Row();
            var net = implant.Implant;
            if (implant.CanInject)
            {
                foreach (var amount in new[] { 1, 5, 10 })
                {
                    buttons.AddChild(SecurityUi.Button(Loc.GetString("prisoner-management-inject", ("amount", amount)),
                        () => SendMessage(new PrisonerImplantActionMessage(net, PrisonerImplantAction.Inject, amount, null))));
                }
            }

            buttons.AddChild(SecurityUi.Button(Loc.GetString("prisoner-management-self-destruct"),
                () => SendMessage(new PrisonerImplantActionMessage(net, PrisonerImplantAction.SelfDestruct, 0, null))));
            box.AddChild(buttons);

            if (implant.CanWarn)
            {
                var warnRow = SecurityUi.Row();
                if (!_warnEdits.TryGetValue(net, out var text))
                {
                    text = new LineEdit { PlaceHolder = Loc.GetString("prisoner-management-warn-placeholder"), HorizontalExpand = true };
                    _warnEdits[net] = text;
                }
                (text.Parent as Control)?.RemoveChild(text);
                warnRow.AddChild(text);
                warnRow.AddChild(SecurityUi.Button(Loc.GetString("prisoner-management-warn"), () =>
                {
                    SendMessage(new PrisonerImplantActionMessage(net, PrisonerImplantAction.Warn, 0, text.Text));
                    text.Text = string.Empty;
                }));
                box.AddChild(warnRow);
            }

            _body.AddChild(new PanelContainer { StyleClasses = { "BackgroundDark" }, Children = { box } });
        }

        SecurityUi.RestoreFocus(focused, cursor);
    }
}

// ─── LaborClaimConsole ─────────────────────────────────────────────────────

public sealed class LaborClaimBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public LaborClaimBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("labor-claim-title"), new Vector2(360, 240), out _body);
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
        if (state is not LaborClaimState s || _body == null)
            return;

        _body.RemoveAllChildren();
        _body.AddChild(SecurityUi.Field(Loc.GetString("labor-claim-status"), s.StatusInfo));
        _body.AddChild(SecurityUi.Field(Loc.GetString("labor-claim-id-points"), s.IdPoints.ToString()));
        _body.AddChild(SecurityUi.Field(Loc.GetString("labor-claim-unclaimed"), s.UnclaimedPoints.ToString()));
        _body.AddChild(SecurityUi.Button(Loc.GetString("labor-claim-claim"),
            () => SendMessage(new LaborClaimActionMessage(LaborClaimAction.ClaimPoints)), s.UnclaimedPoints == 0));
        _body.AddChild(SecurityUi.Button(Loc.GetString("labor-claim-move"),
            () => SendMessage(new LaborClaimActionMessage(LaborClaimAction.MoveShuttle)), !s.CanGoHome));
    }
}

// ─── GulagItemReclaimer ────────────────────────────────────────────────────

public sealed class GulagReclaimerBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public GulagReclaimerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("gulag-reclaimer-title"), new Vector2(340, 300), out _body);
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
        if (state is not GulagReclaimerState s || _body == null)
            return;

        _body.RemoveAllChildren();
        if (s.Mobs.Count == 0)
        {
            _body.AddChild(new Label { Text = Loc.GetString("gulag-reclaimer-empty") });
            return;
        }

        for (var i = 0; i < s.Mobs.Count; i++)
        {
            var mob = s.Mobs[i];
            var row = SecurityUi.Row();
            row.AddChild(new Label { Text = s.Names[i], HorizontalExpand = true, ClipText = true });
            row.AddChild(SecurityUi.Button(Loc.GetString("gulag-reclaimer-release"),
                () => SendMessage(new GulagReclaimerReleaseMessage(mob)), !s.CanReclaim));
            _body.AddChild(row);
        }
    }
}
