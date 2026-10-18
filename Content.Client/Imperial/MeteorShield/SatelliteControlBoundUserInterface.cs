using System.Numerics;
using Content.Shared.Imperial.MeteorShield;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.MeteorShield;

[UsedImplicitly]
public sealed class SatelliteControlBoundUserInterface : BoundUserInterface
{
    private SatelliteControlWindow? _window;

    public SatelliteControlBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<SatelliteControlWindow>();
        _window.OnToggle += id => SendMessage(new SatelliteToggleMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is SatelliteControlUiState sat)
            _window?.UpdateState(sat);
    }
}

/// <summary>Окно консоли спутников 1 в 1 с tgui SatelliteControl из SS13.</summary>
public sealed class SatelliteControlWindow : DefaultWindow
{
    private static readonly Color Good = Color.FromHex("#5baa27");
    private static readonly Color Average = Color.FromHex("#f08f11");
    private static readonly Color Bad = Color.FromHex("#db2828");
    private static readonly Color LabelColor = Color.FromHex("#8b9bb0");

    public event Action<int>? OnToggle;

    private readonly BoxContainer _root = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 8, Margin = new Thickness(6) };

    public SatelliteControlWindow()
    {
        Title = Loc.GetString("satellite-control-title");
        MinSize = SetSize = new Vector2(400, 305);
        Contents.AddChild(new ScrollContainer { HScrollEnabled = false, VerticalExpand = true, Children = { _root } });
    }

    public void UpdateState(SatelliteControlUiState state)
    {
        _root.RemoveAllChildren();

        if (state.MeteorShield)
        {
            var ratio = state.CoverageMax > 0 ? state.Coverage / (float) state.CoverageMax : 0;
            var color = ratio >= 1 ? Good : ratio >= 0.3f ? Average : Bad;
            var bar = new ProgressBar
            {
                MinValue = 0,
                MaxValue = 1,
                Value = Math.Min(ratio, 1),
                MinHeight = 20,
                HorizontalExpand = true,
                ForegroundStyleBoxOverride = new StyleBoxFlat { BackgroundColor = color },
            };

            _root.AddChild(new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Horizontal,
                SeparationOverride = 6,
                Children =
                {
                    new Label { Text = Loc.GetString("satellite-control-coverage"), FontColorOverride = LabelColor, MinWidth = 90 },
                    new Control
                    {
                        HorizontalExpand = true,
                        Children = { bar, new Label { Text = $"{ratio * 100:0}%", HorizontalAlignment = HAlignment.Center } },
                    },
                },
            });
        }

        _root.AddChild(new Label { Text = Loc.GetString("satellite-control-section"), StyleClasses = { "LabelHeading" } });

        var grid = new GridContainer { Columns = 3 };
        foreach (var satellite in state.Satellites)
        {
            var id = satellite.Id;
            var button = new Button
            {
                Text = $"{(satellite.Active ? "☑" : "☐")} #{satellite.Id} {satellite.Mode}",
                ToggleMode = true,
                Pressed = satellite.Active,
                Margin = new Thickness(0, 0, 4, 4),
            };
            button.OnPressed += _ => OnToggle?.Invoke(id);
            grid.AddChild(button);
        }

        _root.AddChild(grid);
    }
}
