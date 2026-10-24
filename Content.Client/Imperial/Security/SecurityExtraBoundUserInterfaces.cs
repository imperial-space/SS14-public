using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

// ─── Принтер плакатов розыска ──────────────────────────────────────────────

public sealed class WantedPrinterBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private readonly ItemList _records = new() { VerticalExpand = true, SelectMode = ItemList.ItemListSelectMode.Single };
    private readonly LineEdit _alias = new() { HorizontalExpand = true };
    private readonly LineEdit _header = new() { HorizontalExpand = true };
    private readonly LineEdit _description = new() { HorizontalExpand = true };
    private readonly Label _status = new();
    private readonly Button _wanted = new() { Text = Loc.GetString("wanted-printer-print-wanted") };
    private readonly Button _missing = new() { Text = Loc.GetString("wanted-printer-print-missing") };
    private readonly Button _rapsheet = new() { Text = Loc.GetString("wanted-printer-print-rapsheet") };

    private WantedPrinterState? _state;
    private string? _selected;

    public WantedPrinterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = new DefaultWindow
        {
            Title = Loc.GetString("wanted-printer-title"),
            MinSize = new Vector2(420, 480),
            SetSize = new Vector2(420, 520),
        };

        var body = SecurityUi.Column(6);
        body.AddChild(new Label { Text = Loc.GetString("wanted-printer-records"), StyleClasses = { "LabelHeading" } });
        body.AddChild(_records);
        body.AddChild(Row(Loc.GetString("wanted-printer-alias"), _alias));
        body.AddChild(Row(Loc.GetString("wanted-printer-header"), _header));
        body.AddChild(Row(Loc.GetString("wanted-printer-description"), _description));

        var buttons = SecurityUi.Row();
        buttons.AddChild(_wanted);
        buttons.AddChild(_missing);
        buttons.AddChild(_rapsheet);
        body.AddChild(buttons);
        body.AddChild(_status);
        _window.Contents.AddChild(body);

        _records.OnItemSelected += args =>
        {
            _selected = _records[args.ItemIndex].Text;
            UpdateButtons();
        };
        _records.OnItemDeselected += _ =>
        {
            _selected = null;
            UpdateButtons();
        };

        _wanted.OnPressed += _ => Print(WantedPrintType.Wanted);
        _missing.OnPressed += _ => Print(WantedPrintType.Missing);
        _rapsheet.OnPressed += _ => Print(WantedPrintType.Rapsheet);

        _window.OnClose += Close;
        _window.OpenCentered();
        UpdateButtons();
    }

    private static BoxContainer Row(string name, Control field)
    {
        var row = SecurityUi.Row();
        row.AddChild(new Label { Text = name + ":", MinWidth = 120 });
        row.AddChild(field);
        return row;
    }

    private void Print(WantedPrintType type)
    {
        if (_selected == null)
            return;

        SendMessage(new WantedPrintMessage(_selected, type, _alias.Text, _header.Text, _description.Text));
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
        if (state is not WantedPrinterState s)
            return;

        _state = s;
        _records.Clear();
        for (var i = 0; i < s.Names.Count; i++)
        {
            var item = _records.AddItem(s.Names[i]);
            if (s.Names[i] == _selected)
                item.Selected = true;
        }

        _status.Text = s.Printing ? Loc.GetString("wanted-printer-status-printing") : string.Empty;
        UpdateButtons();
    }

    private void UpdateButtons()
    {
        var index = _state?.Names.IndexOf(_selected ?? string.Empty) ?? -1;
        var hasCrimes = index >= 0 && _state!.HasCrimes[index];
        var printing = _state?.Printing ?? false;
        _missing.Disabled = index < 0 || printing;
        _wanted.Disabled = !hasCrimes || printing;
        _rapsheet.Disabled = !hasCrimes || printing;
    }
}

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

internal static class SecurityUiExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
