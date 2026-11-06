using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

// ─── Терминал заданий СБ ───────────────────────────────────────────────────

public sealed class SecurityBountyBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public SecurityBountyBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("security-bounty-title"), new Vector2(460, 480), out _body);
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
        if (state is not SecurityBountyState s || _body == null)
            return;

        _body.RemoveAllChildren();

        if (!s.HasPad)
            _body.AddChild(new Label { Text = Loc.GetString("security-bounty-no-pad") });

        if (!string.IsNullOrEmpty(s.StatusReport))
            _body.AddChild(SecurityUi.Field(Loc.GetString("security-bounty-status"), s.StatusReport));

        if (!s.HasId)
        {
            _body.AddChild(new Label { Text = Loc.GetString("security-bounty-insert-id") });
            return;
        }

        _body.AddChild(SecurityUi.Field(Loc.GetString("security-bounty-id"), s.IdName ?? string.Empty));

        if (s.Current is { } current)
        {
            _body.AddChild(SecurityUi.Header(current.Name));
            _body.AddChild(new RichTextLabel { HorizontalExpand = true }.Also(l => l.SetMessage(current.Description)));
            _body.AddChild(SecurityUi.Field(Loc.GetString("security-bounty-progress"), s.CurrentProgress ?? string.Empty));
            _body.AddChild(SecurityUi.Field(Loc.GetString("security-bounty-reward"), current.Reward.ToString()));
        }

        for (var i = 0; i < s.Options.Count; i++)
        {
            var option = s.Options[i];
            var index = i;
            var box = SecurityUi.Column(2);
            box.AddChild(new Label { Text = option.Name, StyleClasses = { "LabelSubText" } });
            box.AddChild(new RichTextLabel { HorizontalExpand = true }.Also(l => l.SetMessage(option.Description)));
            box.AddChild(SecurityUi.Field(Loc.GetString("security-bounty-reward"), option.Reward.ToString()));
            box.AddChild(SecurityUi.Button(Loc.GetString("security-bounty-pick"), () => SendMessage(new SecurityBountyPickMessage(index))));
            _body.AddChild(new PanelContainer { StyleClasses = { "BackgroundDark" }, Children = { box } });
        }

        var row = SecurityUi.Row();
        row.AddChild(SecurityUi.Button(Loc.GetString("security-bounty-request"), () => Send(SecurityBountyAction.Request)));
        row.AddChild(SecurityUi.Button(Loc.GetString("security-bounty-recalc"), () => Send(SecurityBountyAction.Recalc), s.Current == null));
        row.AddChild(SecurityUi.Button(Loc.GetString("security-bounty-send"), () => Send(SecurityBountyAction.Send),
            s.Current == null || s.Sending || !s.HasPad));
        row.AddChild(SecurityUi.Button(Loc.GetString("security-bounty-eject"), () => Send(SecurityBountyAction.Eject)));
        _body.AddChild(row);
    }

    private void Send(SecurityBountyAction action)
    {
        SendMessage(new SecurityBountyActionMessage(action));
    }
}
