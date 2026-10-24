using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Fishing;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>
/// Панель аквариума (tgui Aquarium): температура, среда (блокируются в автоматическом режиме), режим и интервал кормления;
/// карточки рыб (сердечки счастья, достать, погладить, переименовать) и список декора.
/// </summary>
public sealed class ImperialAquariumBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly ImperialFishFluid[] Fluids =
    {
        ImperialFishFluid.Saltwater,
        ImperialFishFluid.Freshwater,
        ImperialFishFluid.Sulphwater,
        ImperialFishFluid.Air,
    };

    private static readonly ImperialAquariumMode[] Modes =
    {
        ImperialAquariumMode.Manual,
        ImperialAquariumMode.Auto,
        ImperialAquariumMode.Safe,
    };

    private static readonly Color CardBackground = Color.FromHex("#243243").WithAlpha(0.5f);
    private static readonly Color CardBorder = Color.FromHex("#574e82");
    private static readonly Color DarkBackground = Color.FromHex("#151326");

    private DefaultWindow? _window;
    private Slider _temperature = default!;
    private Label _temperatureLabel = default!;
    private readonly Dictionary<ImperialFishFluid, Button> _fluidButtons = new();
    private OptionButton _mode = default!;
    private Control _modeHelp = default!;
    private Label _interval = default!;
    private Control _tempLock = default!;
    private Control _fluidLock = default!;
    private GridContainer _fishGrid = default!;
    private BoxContainer _props = default!;
    private ImperialAquariumBoundUserInterfaceState? _state;
    private string? _signature;
    private bool _updating;

    protected override void Open()
    {
        base.Open();
        var sprite = EntMan.System<SpriteSystem>();
        _window = this.CreateWindow<DefaultWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.SetSize = new Vector2(765, 520);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };

        // Настройки: температура, среда, режим.
        var settings = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, MinHeight = 150 };

        var tempBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, VerticalAlignment = Control.VAlignment.Center };
        _temperatureLabel = new Label { Align = Label.AlignMode.Center, StyleClasses = { "LabelHeading" } };
        _temperature = new Slider { MinWidth = 160, Rounded = true };
        _temperature.OnReleased += _ =>
        {
            if (!_updating)
                SendMessage(new ImperialAquariumSetTemperatureMessage(_temperature.Value));
        };
        _temperature.OnValueChanged += _ => _temperatureLabel.Text = $"{_temperature.Value:0} K";
        tempBox.AddChild(_temperatureLabel);
        tempBox.AddChild(_temperature);
        _tempLock = ImperialFishingUi.Locked(sprite);
        settings.AddChild(Lockable(Loc.GetString("imperial-aquarium-ui-temperature"), tempBox, _tempLock));

        var fluidBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        foreach (var fluid in Fluids)
        {
            var button = new Button
            {
                Text = Loc.GetString($"imperial-aquarium-fluid-{fluid.ToString().ToLowerInvariant()}"),
                ToggleMode = true,
                HorizontalExpand = true,
                StyleClasses = { "ButtonSquare" },
            };
            var captured = fluid;
            button.OnPressed += _ => SendMessage(new ImperialAquariumSetFluidMessage(captured));
            fluidBox.AddChild(button);
            _fluidButtons[fluid] = button;
        }

        _fluidLock = ImperialFishingUi.Locked(sprite);
        settings.AddChild(Lockable(Loc.GetString("imperial-aquarium-ui-fluid"), fluidBox, _fluidLock));

        var modeBox = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(0, 8, 0, 0) };
        _mode = new OptionButton { HorizontalExpand = true };
        foreach (var mode in Modes)
            _mode.AddItem(Loc.GetString($"imperial-aquarium-mode-{mode.ToString().ToLowerInvariant()}"), (int) mode);
        _mode.OnItemSelected += args =>
        {
            if (_updating)
                return;
            _mode.SelectId(args.Id);
            SendMessage(new ImperialAquariumSetModeMessage((ImperialAquariumMode) args.Id));
        };
        var modeRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, HorizontalExpand = true };
        modeRow.AddChild(_mode);
        _modeHelp = ImperialFishingUi.Texture(sprite, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/Nano/help.png")), 22);
        _modeHelp.Modulate = Color.FromHex("#2185d0");
        _modeHelp.MouseFilter = Control.MouseFilterMode.Pass;
        _modeHelp.Margin = new Thickness(4, 0, 0, 0);
        modeRow.AddChild(_modeHelp);
        modeBox.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-aquarium-ui-mode"), modeRow, 120));

        var intervalRow = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        var minus = new Button { Text = "-", StyleClasses = { "ButtonSquare" } };
        minus.OnPressed += _ => { if (_state != null) SendMessage(new ImperialAquariumSetFeedingIntervalMessage(_state.FeedingIntervalMinutes - 1)); };
        var plus = new Button { Text = "+", StyleClasses = { "ButtonSquare" } };
        plus.OnPressed += _ => { if (_state != null) SendMessage(new ImperialAquariumSetFeedingIntervalMessage(_state.FeedingIntervalMinutes + 1)); };
        _interval = new Label { MinWidth = 80, Align = Label.AlignMode.Center };
        intervalRow.AddChild(minus);
        intervalRow.AddChild(_interval);
        intervalRow.AddChild(plus);
        modeBox.AddChild(ImperialFishingUi.LabeledItem(Loc.GetString("imperial-aquarium-ui-feeding"), intervalRow, 120));

        var settingsSection = ImperialFishingUi.Section(Loc.GetString("imperial-aquarium-ui-settings"), modeBox);
        settingsSection.HorizontalExpand = true;
        settings.AddChild(settingsSection);
        root.AddChild(settings);

        // Рыбы и декор.
        var lower = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, VerticalExpand = true };
        _fishGrid = new GridContainer { Columns = 2, HorizontalExpand = true };
        var fishScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        fishScroll.AddChild(_fishGrid);
        var fishSection = ImperialFishingUi.Section(Loc.GetString("imperial-aquarium-ui-fish"), fishScroll);
        fishSection.HorizontalExpand = true;
        fishSection.SizeFlagsStretchRatio = 3;
        fishSection.VerticalExpand = true;
        lower.AddChild(fishSection);

        _props = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var propScroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        propScroll.AddChild(_props);
        var propSection = ImperialFishingUi.Section(Loc.GetString("imperial-aquarium-ui-props"), propScroll);
        propSection.HorizontalExpand = true;
        propSection.SizeFlagsStretchRatio = 1;
        propSection.VerticalExpand = true;
        lower.AddChild(propSection);
        root.AddChild(lower);

        _window.Contents.AddChild(root);
        if (_state != null)
            Apply(_state);
    }

    /// <summary>Секция с затемнением «ЗАБЛОКИРОВАНО» поверх содержимого.</summary>
    private static Control Lockable(string title, Control content, Control dimmer)
    {
        var stack = new Control { HorizontalExpand = true, VerticalExpand = true };
        stack.AddChild(content);
        stack.AddChild(dimmer);
        var section = ImperialFishingUi.Section(title, stack);
        section.HorizontalExpand = true;
        return section;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialAquariumBoundUserInterfaceState aquarium)
            return;

        _state = aquarium;
        if (_window != null)
            Apply(aquarium);
    }

    private void Apply(ImperialAquariumBoundUserInterfaceState state)
    {
        _updating = true;
        var locked = state.Mode == ImperialAquariumMode.Auto;
        _temperature.MinValue = state.MinTemperature;
        _temperature.MaxValue = state.MaxTemperature;
        if (!_temperature.Grabbed)
            _temperature.Value = state.Temperature;
        _temperatureLabel.Text = $"{_temperature.Value:0} K";
        _tempLock.Visible = locked;
        _fluidLock.Visible = locked;

        foreach (var (fluid, button) in _fluidButtons)
            button.Pressed = fluid == state.Fluid;

        _mode.SelectId((int) state.Mode);
        _modeHelp.ToolTip = Loc.GetString($"imperial-aquarium-mode-{state.Mode.ToString().ToLowerInvariant()}-desc");
        _interval.Text = Loc.GetString("imperial-aquarium-ui-feeding-value", ("value", MathF.Round(state.FeedingIntervalMinutes)));
        _updating = false;

        // Списки перестраиваются только при изменении, чтобы не сбивать ввод имени.
        var signature = string.Join("|", state.Fish.Select(f => $"{f.Entity}:{f.Name}:{f.Alive}:{f.Happiness}"))
                        + "#" + string.Join("|", state.Props.Select(p => $"{p.Entity}:{p.Name}"));
        if (signature == _signature)
            return;

        _signature = signature;
        _fishGrid.RemoveAllChildren();
        foreach (var fish in state.Fish)
            _fishGrid.AddChild(FishCard(fish));

        _props.RemoveAllChildren();
        var sprite = EntMan.System<SpriteSystem>();
        foreach (var prop in state.Props)
        {
            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
            row.AddChild(ImperialFishingUi.Icon(sprite, prop.Proto, 40));
            row.AddChild(new Label
            {
                Text = prop.Name.Length > 0 ? prop.Name[..1].ToUpperInvariant() + prop.Name[1..] : prop.Name,
                Margin = new Thickness(6, 0, 0, 0),
                VerticalAlignment = Control.VAlignment.Center,
                ClipText = true,
                HorizontalExpand = true,
            });
            var button = new Button { HorizontalExpand = true, StyleClasses = { "ButtonSquare" } };
            button.AddChild(row);
            var net = prop.Entity;
            button.OnPressed += _ => SendMessage(new ImperialAquariumRemoveItemMessage(net));
            _props.AddChild(button);
        }
    }

    private Control FishCard(ImperialAquariumFishEntry fish)
    {
        var sprite = EntMan.System<SpriteSystem>();
        var card = ImperialFishingUi.Card(CardBackground, CardBorder);
        card.HorizontalExpand = true;
        card.MinHeight = 100;

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
        var top = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        top.AddChild(ImperialFishingUi.Icon(sprite, fish.Proto, 40, DarkBackground));

        var info = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, HorizontalExpand = true, Margin = new Thickness(6, 0, 0, 0) };
        info.AddChild(new Label { Text = fish.Name, StyleClasses = { "LabelHeading" }, ClipText = true, FontColorOverride = Color.White });
        var status = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal };
        if (fish.Alive)
        {
            // CalculateHappiness: пять сердечек, полные до уровня счастья включительно.
            for (var i = 0; i < 5; i++)
            {
                var heart = ImperialFishingUi.Texture(sprite, new SpriteSpecifier.Rsi(ImperialFishingUi.HeartsRsi, fish.Happiness >= i ? "full_heart" : "empty_heart"), 24);
                status.AddChild(heart);
            }
        }
        else
        {
            var skull = ImperialFishingUi.Texture(sprite, new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/die.svg.192dpi.png")), 20);
            skull.Margin = new Thickness(8, 2, 0, 0);
            status.AddChild(skull);
        }

        info.AddChild(status);
        top.AddChild(info);

        var net = fish.Entity;
        var remove = new TextureButton
        {
            TextureNormal = sprite.Frame0(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/eject.svg.192dpi.png"))),
            Scale = new Vector2(0.5f, 0.5f),
            ToolTip = Loc.GetString("imperial-aquarium-ui-take"),
            VerticalAlignment = Control.VAlignment.Top,
        };
        remove.OnPressed += _ => SendMessage(new ImperialAquariumRemoveItemMessage(net));
        top.AddChild(remove);
        root.AddChild(top);

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var pet = new Button { Text = Loc.GetString("imperial-aquarium-ui-pet"), HorizontalExpand = true, StyleClasses = { "ButtonSquare" } };
        pet.OnPressed += _ => SendMessage(new ImperialAquariumPetFishMessage(net));
        buttons.AddChild(pet);

        var rename = new LineEdit
        {
            PlaceHolder = Loc.GetString("imperial-aquarium-ui-rename"),
            HorizontalExpand = true,
            Margin = new Thickness(4, 0, 0, 0),
        };
        rename.OnTextEntered += args => SendMessage(new ImperialAquariumRenameFishMessage(net, args.Text));
        buttons.AddChild(rename);
        root.AddChild(buttons);

        card.AddChild(root);
        return card;
    }
}
