using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Lavaland.Gps;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Lavaland.Gps;

[UsedImplicitly]
public sealed class ImperialGpsBoundUserInterface : BoundUserInterface
{
    private ImperialGpsWindow? _window;

    public ImperialGpsBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ImperialGpsWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ImperialGpsUiState gps)
            _window?.UpdateState(gps);
    }
}

/// <summary>Окно GPS 1 в 1 с tgui Gps из SS13 (470×700).</summary>
public sealed class ImperialGpsWindow : DefaultWindow
{
    private static readonly string[] Arrows = { "↑", "↗", "→", "↘", "↓", "↙", "←", "↖" };

    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private readonly BoxContainer _root = Tgui.VBox(8);
    private readonly LineEdit _search = new() { PlaceHolder = "Поиск по имени...", MinWidth = 200 };
    private readonly LineEdit _rename = new() { MinWidth = 140 };
    private ImperialGpsUiState? _state;
    private bool _renaming;

    public ImperialGpsWindow()
    {
        Title = Loc.GetString("imperial-gps-ui-title");
        MinSize = SetSize = new Vector2(470, 700);
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));

        _search.OnTextChanged += _ => Rebuild();
        _rename.OnTextEntered += args =>
        {
            _renaming = false;
            OnMessage?.Invoke(new ImperialGpsRenameMessage(args.Text));
            Rebuild();
        };
    }

    public void UpdateState(ImperialGpsUiState state)
    {
        _state = state;
        Rebuild();
    }

    private void Send(BoundUserInterfaceMessage message)
    {
        OnMessage?.Invoke(message);
    }

    private void Rebuild()
    {
        if (_state is not { } state)
            return;

        var searchFocus = _search.HasKeyboardFocus();
        var renameFocus = _rename.HasKeyboardFocus();
        _search.Orphan();
        _rename.Orphan();
        _root.RemoveAllChildren();

        // Control
        Control tagControl;
        if (_renaming)
        {
            if (!renameFocus)
                _rename.Text = state.Tag;
            tagControl = _rename;
        }
        else
        {
            tagControl = Tgui.Button(state.Tag, () =>
            {
                _renaming = true;
                Rebuild();
                _rename.GrabKeyboardFocus();
            }, icon: "pencil-alt");
        }

        var scanMode = Tgui.Button(state.Updating ? "AUTO" : "MANUAL", () => Send(new ImperialGpsUpdatingMessage()),
            color: state.Updating ? null : Tgui.Bad, icon: state.Updating ? "unlock" : "lock");
        var scanRow = Tgui.HBox(4, scanMode);
        if (!state.Updating)
            scanRow.AddChild(Tgui.Button(Loc.GetString("imperial-gps-ui-refresh"), () => Send(new ImperialGpsRefreshMessage()), icon: "sync"));

        var control = Tgui.LabeledList(
            (Loc.GetString("imperial-gps-ui-tag"), tagControl),
            (Loc.GetString("imperial-gps-ui-scan-mode"), scanRow),
            (Loc.GetString("imperial-gps-ui-range"), Tgui.Button(state.GlobalMode ? "MAXIMUM" : "LOCAL",
                () => Send(new ImperialGpsGlobalModeMessage()), color: state.GlobalMode ? null : Tgui.Green, icon: "sync")));

        var power = Tgui.Button(state.Power ? "On" : "Off", () => Send(new ImperialGpsPowerMessage()),
            color: state.Power ? Tgui.Green : null, icon: "power-off");
        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-gps-ui-control"), power, control));
        if (renameFocus)
            _rename.GrabKeyboardFocus();

        if (!state.Power)
            return;

        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-gps-ui-location"), null,
            Tgui.Text($"{state.CurrentArea} ({state.CurrentCoords})", size: 18)));

        // Detected Signals: сначала с расстоянием, потом по алфавиту.
        var search = _search.Text.Trim();
        var signals = state.Signals
            .Where(s => search.Length == 0 || s.Tag.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderBy(s => s.Distance == null)
            .ThenBy(s => s.Tag)
            .ToList();

        var table = new GridContainer { Columns = 3, HSeparationOverride = 12, VSeparationOverride = 4, HorizontalExpand = true };
        table.AddChild(Tgui.Text(Loc.GetString("imperial-gps-ui-name"), bold: true));
        table.AddChild(Tgui.Text(Loc.GetString("imperial-gps-ui-direction"), bold: true));
        table.AddChild(Tgui.Text(Loc.GetString("imperial-gps-ui-coords"), bold: true));
        foreach (var signal in signals)
        {
            var name = Tgui.Text(signal.Tag, Tgui.Label, bold: true);
            name.HorizontalExpand = true;
            table.AddChild(name);

            var direction = string.Empty;
            if (signal.Degrees is { } deg)
                direction += Arrows[(int) MathF.Round(deg / 45f) % 8] + " ";
            if (signal.Distance is { } dist)
                direction += $"{dist}m";
            // opacity по расстоянию, как в tgui: дальние сигналы бледнее.
            var alpha = signal.Distance is { } d ? Math.Clamp(1.2f / MathF.Log(MathF.E + d / 20f), 0.3f, 1f) : 1f;
            table.AddChild(Tgui.Text(direction, Tgui.TextColor.WithAlpha(alpha)));
            table.AddChild(Tgui.Text(signal.Coords, Tgui.TextColor.WithAlpha(alpha)));
        }

        var content = Tgui.VBox(4, table);
        content.VerticalExpand = true;
        _root.AddChild(Tgui.MakeSection(Loc.GetString("imperial-gps-ui-signals"), _search, content));
        if (searchFocus)
            _search.GrabKeyboardFocus();
    }
}
