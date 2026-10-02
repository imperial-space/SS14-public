using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.BSA;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.BSA;

[UsedImplicitly]
public sealed class BSABoundUserInterface : BoundUserInterface
{
    private BSAConsoleWindow? _window;

    public BSABoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BSAConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BSAConsoleUiState bsa)
            _window?.UpdateState(bsa);
    }
}

/// <summary>Окно консоли БСА 1 в 1 с tgui BluespaceArtillery из SS13 (400×220).</summary>
public sealed class BSAConsoleWindow : DefaultWindow
{
    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private readonly BoxContainer _root = Tgui.VBox(8);
    private bool _picking;
    private BSAConsoleUiState? _state;

    public BSAConsoleWindow()
    {
        Title = Loc.GetString("bsa-console-title");
        MinSize = SetSize = new Vector2(400, 260);
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root));
    }

    public void UpdateState(BSAConsoleUiState state)
    {
        _state = state;
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state is not { } state)
            return;

        _root.RemoveAllChildren();

        if (!string.IsNullOrEmpty(state.Notice))
            _root.AddChild(Tgui.NoticeBox(state.Notice));

        if (!state.Connected)
        {
            _root.AddChild(Tgui.MakeSection(null, null, Tgui.LabeledList(
                (Loc.GetString("bsa-console-maintenance"),
                    Tgui.Button("🔧 " + Loc.GetString("bsa-console-complete-deployment"),
                        () => OnMessage?.Invoke(new BSABuildMessage()))))));
            return;
        }

        // Section «Target» с кнопкой-прицелом.
        var crosshair = Tgui.Button("⌖", () =>
        {
            _picking = !_picking;
            Rebuild();
        }, color: _picking ? Tgui.Green : null, disabled: !state.Unlocked, fontSize: 14, bold: true,
            tooltip: Loc.GetString("bsa-console-recalibrate"));

        var target = Tgui.Text(state.Target ?? Loc.GetString("bsa-console-no-target"),
            state.Target != null ? Tgui.Average : Tgui.Bad, size: 25);
        _root.AddChild(Tgui.MakeSection(Loc.GetString("bsa-console-target"), crosshair, target));

        if (_picking && state.Unlocked)
        {
            var list = Tgui.VBox(2);
            if (state.Targets.Count == 0)
                list.AddChild(Tgui.Text(Loc.GetString("bsa-console-no-beacons"), Tgui.Label));

            foreach (var (uid, name) in state.Targets)
            {
                list.AddChild(Tgui.Button(name, () =>
                {
                    _picking = false;
                    OnMessage?.Invoke(new BSASetTargetMessage(uid));
                }, fluid: true));
            }

            _root.AddChild(Tgui.MakeSection(Loc.GetString("bsa-console-select-target"), null,
                new ScrollContainer { HScrollEnabled = false, MinHeight = 120, Children = { list } }));
        }

        Control fireSection;
        if (state.Unlocked)
        {
            var fire = Tgui.Button(Loc.GetString("bsa-console-fire"), () => OnMessage?.Invoke(new BSAFireMessage()),
                color: Tgui.Red, disabled: state.Target == null, fluid: true, fontSize: 30, bold: true);
            fire.MinHeight = 52;
            fireSection = fire;
        }
        else
        {
            fireSection = Tgui.VBox(6,
                Tgui.Text(Loc.GetString("bsa-console-locked"), Tgui.Bad, size: 18),
                new RichTextLabel { Text = Loc.GetString("bsa-console-locked-hint") });
        }

        _root.AddChild(Tgui.MakeSection(null, null, fireSection));
    }
}
