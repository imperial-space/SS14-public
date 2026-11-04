using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

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
