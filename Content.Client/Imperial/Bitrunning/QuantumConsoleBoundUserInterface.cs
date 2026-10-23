using System.Numerics;
using Content.Shared.Imperial.Bitrunning;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

/// <summary>Окно квантовой консоли (tgui QuantumConsole): выбор домена, статус сервера и аватаров.</summary>
public sealed class QuantumConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private QuantumConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<QuantumConsoleWindow>();
        _window.OnSetDomain += id => SendMessage(new QuantumConsoleSetDomainMessage(id));
        _window.OnRandom += () => SendMessage(new QuantumConsoleRandomDomainMessage());
        _window.OnStop += () => SendMessage(new QuantumConsoleStopDomainMessage());
        _window.OnRefresh += () => SendMessage(new QuantumConsoleRefreshMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is QuantumConsoleState console)
            _window?.Update(console);
    }
}

public sealed class QuantumConsoleWindow : DefaultWindow
{
    public event Action<string>? OnSetDomain;
    public event Action? OnRandom;
    public event Action? OnStop;
    public event Action? OnRefresh;

    private readonly Label _status = new();
    private readonly Label _points = new();
    private readonly Label _loaded = new();
    private readonly Label _retries = new();
    private readonly Button _random = new() { Text = Loc.GetString("bitrunning-console-random") };
    private readonly Button _stop = new() { Text = Loc.GetString("bitrunning-console-stop") };
    private readonly BoxContainer _avatars = new() { Orientation = BoxContainer.LayoutOrientation.Vertical };
    private readonly BoxContainer _domains = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 6 };

    public QuantumConsoleWindow()
    {
        Title = Loc.GetString("bitrunning-console-title");
        MinSize = new Vector2(560, 600);
        SetSize = new Vector2(620, 680);

        var refresh = new Button { Text = Loc.GetString("bitrunning-console-refresh") };
        _random.OnPressed += _ => OnRandom?.Invoke();
        _stop.OnPressed += _ => OnStop?.Invoke();
        refresh.OnPressed += _ => OnRefresh?.Invoke();

        var buttons = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 6 };
        buttons.AddChild(_random);
        buttons.AddChild(_stop);
        buttons.AddChild(refresh);

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 4 };
        root.AddChild(_status);
        root.AddChild(_points);
        root.AddChild(_loaded);
        root.AddChild(_retries);
        root.AddChild(buttons);
        root.AddChild(new Label { Text = Loc.GetString("bitrunning-console-avatars"), StyleClasses = { "LabelHeading" } });
        root.AddChild(_avatars);
        root.AddChild(new Label { Text = Loc.GetString("bitrunning-console-domains"), StyleClasses = { "LabelHeading" } });

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_domains);
        root.AddChild(scroll);
        Contents.AddChild(root);
    }

    public void Update(QuantumConsoleState state)
    {
        _domains.RemoveAllChildren();
        _avatars.RemoveAllChildren();

        if (!state.Connected)
        {
            _status.Text = Loc.GetString("bitrunning-console-not-connected");
            _points.Text = _loaded.Text = _retries.Text = string.Empty;
            _random.Disabled = _stop.Disabled = true;
            return;
        }

        _status.Text = Loc.GetString("bitrunning-console-ready", ("ready", state.Ready));
        _points.Text = Loc.GetString("bitrunning-console-points", ("points", state.Points));
        _loaded.Text = state.GeneratedDomain == null
            ? Loc.GetString("bitrunning-console-none-loaded")
            : Loc.GetString("bitrunning-console-loaded", ("domain", state.Domains.Find(d => d.Id == state.GeneratedDomain)?.Name ?? state.GeneratedDomain));
        _retries.Text = state.GeneratedDomain == null
            ? string.Empty
            : $"{Loc.GetString("bitrunning-console-retries", ("retries", state.RetriesLeft))}   {Loc.GetString("bitrunning-console-occupants", ("count", state.Occupants))}";

        var canLoad = state.Ready && state.GeneratedDomain == null && state.Occupants == 0;
        _random.Disabled = !canLoad || state.Points < 1;
        _stop.Disabled = state.GeneratedDomain == null;

        foreach (var avatar in state.Avatars)
        {
            _avatars.AddChild(new Label
            {
                Text = Loc.GetString("bitrunning-console-avatar",
                    ("name", avatar.Name), ("pilot", avatar.Pilot), ("health", (int) avatar.Health),
                    ("brute", (int) avatar.Brute), ("burn", (int) avatar.Burn), ("tox", (int) avatar.Toxin), ("oxy", (int) avatar.Airloss)),
            });
        }

        foreach (var domain in state.Domains)
        {
            var panel = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical };
            var header = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 8 };
            var name = new Label { Text = domain.Name, StyleClasses = { "LabelSubText" }, HorizontalExpand = true };
            name.FontColorOverride = domain.Id == state.GeneratedDomain ? Color.LightGreen : Color.White;
            header.AddChild(name);

            var load = new Button
            {
                Text = Loc.GetString("bitrunning-console-load"),
                Disabled = !canLoad || state.Points < domain.Cost,
            };
            var id = domain.Id;
            load.OnPressed += _ => OnSetDomain?.Invoke(id);
            header.AddChild(load);
            panel.AddChild(header);

            var tags = new List<string>
            {
                Loc.GetString("bitrunning-console-cost", ("cost", domain.Cost)),
                Loc.GetString("bitrunning-console-reward", ("reward", domain.Reward)),
                Loc.GetString("bitrunning-console-difficulty", ("difficulty", (int) domain.Difficulty)),
            };
            if (domain.Modular)
                tags.Add(Loc.GetString("bitrunning-console-modular"));
            if (domain.Secondary)
                tags.Add(Loc.GetString("bitrunning-console-secondary"));

            panel.AddChild(new Label { Text = string.Join(" · ", tags), FontColorOverride = Color.Gray });
            panel.AddChild(new RichTextLabel { Text = domain.Description });
            panel.AddChild(new Content.Client.UserInterface.Controls.HLine());
            _domains.AddChild(panel);
        }
    }
}
