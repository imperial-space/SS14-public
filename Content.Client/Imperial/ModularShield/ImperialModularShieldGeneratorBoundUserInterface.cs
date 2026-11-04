using System.Numerics;
using Content.Client.Imperial.Fishing;
using Content.Shared.Imperial.ModularShield;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.ModularShield;

/// <summary>Окно генератора модульного щита (tgui ModularShieldGen).</summary>
public sealed class ImperialModularShieldGeneratorBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private DefaultWindow? _window;
    private Label _strengthTitle = default!;
    private (Control Root, ProgressBar Bar, Label Text) _strength;
    private (Control Root, ProgressBar Bar, Label Text) _regen;
    private (Control Root, ProgressBar Bar, Label Text) _radius;
    private Label _radiusValue = default!;
    private Button _radiusMinus = default!;
    private Button _radiusPlus = default!;
    private Button _exterior = default!;
    private Button _power = default!;
    private ImperialModularShieldGeneratorState? _state;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = Loc.GetString("imperial-modular-shield-ui-title");
        _window.SetSize = new Vector2(690, 260);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };

        var left = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SizeFlagsStretchRatio = 2 };
        _strength = ShieldUi.Bar();
        var strengthSection = ImperialFishingUi.Section(Loc.GetString("imperial-modular-shield-ui-strength"), _strength.Root);
        _strengthTitle = FindHeading(strengthSection)!;
        left.AddChild(strengthSection);

        var regenBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        _regen = ShieldUi.Bar();
        _radius = ShieldUi.Bar();
        regenBox.AddChild(_regen.Root);
        regenBox.AddChild(_radius.Root);
        left.AddChild(ImperialFishingUi.Section(Loc.GetString("imperial-modular-shield-ui-regen-radius"), regenBox));
        root.AddChild(left);

        var right = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, SizeFlagsStretchRatio = 1 };
        var settings = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var radiusRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        _radiusMinus = new Button { Text = "-", StyleClasses = { "ButtonSquare" } };
        _radiusMinus.OnPressed += _ => SetRadius(-1);
        _radiusValue = new Label { MinWidth = 50, Align = Label.AlignMode.Center };
        _radiusPlus = new Button { Text = "+", StyleClasses = { "ButtonSquare" } };
        _radiusPlus.OnPressed += _ => SetRadius(1);
        radiusRow.AddChild(_radiusMinus);
        radiusRow.AddChild(_radiusValue);
        radiusRow.AddChild(_radiusPlus);
        settings.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-modular-shield-ui-set-radius"), radiusRow, 100));

        _exterior = new Button { StyleClasses = { "ButtonSquare" } };
        _exterior.OnPressed += _ => SendMessage(new ImperialModularShieldToggleExteriorMessage());
        settings.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-modular-shield-ui-limitations"), _exterior, 100));
        right.AddChild(ImperialFishingUi.Section(Loc.GetString("imperial-modular-shield-ui-settings"), settings));

        _power = new Button { ToggleMode = true };
        _power.OnPressed += _ => SendMessage(new ImperialModularShieldToggleMessage());
        right.AddChild(ImperialFishingUi.Section(null, ImperialFishingUi.LabeledItem(Loc.GetString("imperial-modular-shield-ui-power"), _power, 100)));
        root.AddChild(right);

        _window.Contents.AddChild(root);
        if (_state != null)
            Apply(_state);
    }

    private static Label? FindHeading(Control control)
    {
        foreach (var child in control.Children)
        {
            if (child is Label label && label.StyleClasses.Contains("LabelHeading"))
                return label;
            if (FindHeading(child) is { } found)
                return found;
        }

        return null;
    }

    private void SetRadius(int delta)
    {
        if (_state == null)
            return;

        var radius = Math.Clamp(MathF.Round(_state.CurrentRadius) + delta, 3, MathF.Max(3, _state.MaxRadius));
        SendMessage(new ImperialModularShieldSetRadiusMessage(radius));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialModularShieldGeneratorState gen)
            return;

        _state = gen;
        if (_window != null)
            Apply(gen);
    }

    private void Apply(ImperialModularShieldGeneratorState s)
    {
        _strengthTitle.FontColorOverride = s.Recovering ? Color.Red : Color.White;
        ShieldUi.Set(_strength.Bar, _strength.Text, s.CurrentStrength, s.MaxStrength, $"{ShieldUi.Num(s.CurrentStrength)}/{ShieldUi.Num(s.MaxStrength)}");
        ShieldUi.Set(_regen.Bar, _regen.Text, s.CurrentRegeneration, s.MaxRegeneration,
            Loc.GetString("imperial-modular-shield-ui-regen", ("current", ShieldUi.Num(s.CurrentRegeneration)), ("max", ShieldUi.Num(s.MaxRegeneration))));
        ShieldUi.Set(_radius.Bar, _radius.Text, s.CurrentRadius, s.MaxRadius,
            Loc.GetString("imperial-modular-shield-ui-radius", ("current", ShieldUi.Num(s.CurrentRadius)), ("max", ShieldUi.Num(s.MaxRadius))));

        _radiusValue.Text = ShieldUi.Num(s.CurrentRadius);
        _radiusMinus.Disabled = s.Active;
        _radiusPlus.Disabled = s.Active;
        _exterior.Disabled = s.Active;
        _exterior.Text = Loc.GetString(s.ExteriorOnly ? "imperial-modular-shield-ui-exterior" : "imperial-modular-shield-ui-interior");
        _power.Disabled = s.Recovering || s.Initiating;
        _power.Pressed = s.Active;
        _power.Text = Loc.GetString(s.Active ? "imperial-modular-shield-ui-on" : "imperial-modular-shield-ui-off");
    }
}
