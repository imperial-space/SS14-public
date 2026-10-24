using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

[UsedImplicitly]
public sealed class QuantumConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private QuantumConsoleWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<QuantumConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is QuantumConsoleState console)
            _window?.Update(console);
    }
}

/// <summary>
/// Окно квантовой консоли 1 в 1 с tgui QuantumConsole (500×500).
/// Каркас строится один раз; сервер присылает состояние раз в 2 секунды, и при неизменных данных окно не трогается,
/// иначе перезаполняются только списки — без пересоздания прокрутки и секций.
/// </summary>
public sealed class QuantumConsoleWindow : DefaultWindow
{
    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private static readonly Color Pink = Color.FromHex("#e03997");
    private static readonly Color Gold = Color.FromHex("#e9b934");

    private readonly BoxContainer _root = Tgui.VBox(6);
    private readonly Control _notConnected;
    private readonly Control _loading;
    private readonly BoxContainer _main = Tgui.VBox(6);

    private readonly BoxContainer _domainButtons = Tgui.HBox(6);
    private readonly BoxContainer _domainList = Tgui.VBox(4);
    private readonly BoxContainer _avatarButtons = Tgui.HBox(6);
    private readonly BoxContainer _avatarTable = Tgui.VBox(4);
    private readonly BoxContainer _footer = Tgui.HBox(6);

    /// <summary>useSharedState('tab').</summary>
    private BitrunningDifficulty _tab = BitrunningDifficulty.None;
    private readonly HashSet<string> _expanded = new();
    private QuantumConsoleState? _state;
    private string? _signature;

    public QuantumConsoleWindow()
    {
        Title = Loc.GetString("bitrunning-console-title");
        MinSize = SetSize = new Vector2(500, 500);
        _root.Margin = new Thickness(6);
        _root.VerticalExpand = true;

        _notConnected = Tgui.NoticeBox(Loc.GetString("bitrunning-console-not-connected"), Tgui.Bad);
        _loading = LoadingScreen();

        var scroll = new ScrollContainer { HScrollEnabled = false, VerticalExpand = true };
        scroll.AddChild(_domainList);
        var domains = Tgui.MakeSection(Loc.GetString("bitrunning-console-domains"), _domainButtons, scroll);
        domains.VerticalExpand = true;

        _main.VerticalExpand = true;
        _main.AddChild(domains);
        _main.AddChild(Tgui.MakeSection(Loc.GetString("bitrunning-console-avatars"), _avatarButtons, _avatarTable));
        _main.AddChild(Tgui.MakeSection(null, null, _footer));

        _root.AddChild(_notConnected);
        _root.AddChild(_loading);
        _root.AddChild(_main);
        Contents.AddChild(Tgui.Window(_root, scrollable: false));
    }

    private static Color DifficultyColor(BitrunningDifficulty difficulty)
    {
        return difficulty switch
        {
            BitrunningDifficulty.Low => Tgui.Yellow,
            BitrunningDifficulty.Medium => Tgui.Average,
            BitrunningDifficulty.High => Tgui.Bad,
            _ => Tgui.Green,
        };
    }

    public void Update(QuantumConsoleState state)
    {
        var signature = Signature(state);
        if (signature == _signature)
            return;

        _signature = signature;
        _state = state;
        Refill();
    }

    /// <summary>Всё, что видно в окне: одинаковая строка — одинаковая картинка.</summary>
    private static string Signature(QuantumConsoleState state)
    {
        var sb = new StringBuilder();
        sb.Append(state.Connected).Append('|').Append(state.Ready).Append('|').Append(state.GeneratedDomain)
            .Append('|').Append(state.Occupants).Append('|').Append(state.Points).Append('|').Append(state.Randomized)
            .Append('|').Append(state.RetriesLeft).Append('|').Append(state.Broadcasting).Append('|').Append(state.BroadcastOnCooldown);
        foreach (var d in state.Domains)
        {
            sb.Append(';').Append(d.Id).Append(',').Append(d.Name).Append(',').Append(d.Reward);
        }

        foreach (var a in state.Avatars)
        {
            sb.Append(';').Append(a.Name).Append(',').Append(a.Pilot).Append(',').Append((int) a.Health)
                .Append(',').Append(a.Brute > 50).Append(a.Burn > 50).Append(a.Toxin > 50).Append(a.Airloss > 50)
                .Append(a.Brute + a.Burn + a.Toxin + a.Airloss == 0);
        }

        return sb.ToString();
    }

    private void Refill()
    {
        if (_state is not { } state)
            return;

        _notConnected.Visible = !state.Connected;
        _loading.Visible = state.Connected && !state.Ready;
        _main.Visible = state.Connected && state.Ready;
        if (!_main.Visible)
            return;

        FillDomainButtons(state);
        FillDomains(state);
        FillAvatars(state);
        FillFooter(state);
    }

    /// <summary>LoadingScreen: вращающийся ящик с инструментами и «Please wait...».</summary>
    private static Control LoadingScreen()
    {
        var box = Tgui.VBox(8,
            new BitrunningSpinner("toolbox", Tgui.Blue, 48) { HorizontalAlignment = HAlignment.Center },
            Tgui.Text(Loc.GetString("bitrunning-console-wait"), align: HAlignment.Center));
        box.VerticalAlignment = VAlignment.Center;
        box.HorizontalAlignment = HAlignment.Center;
        box.VerticalExpand = true;
        return box;
    }

    private void FillDomainButtons(QuantumConsoleState state)
    {
        _domainButtons.RemoveAllChildren();
        var broadcast = TguiLayout.Checkbox(Loc.GetString("bitrunning-console-broadcast"), state.Broadcasting,
            () => OnMessage?.Invoke(new QuantumConsoleBroadcastMessage()));
        broadcast.Disabled = state.BroadcastOnCooldown;
        broadcast.ToolTip = Loc.GetString("bitrunning-console-broadcast-tooltip");
        _domainButtons.AddChild(broadcast);

        _domainButtons.AddChild(BitrunningTgui.Button(Loc.GetString("bitrunning-console-random"), "random",
            onPressed: () => OnMessage?.Invoke(new QuantumConsoleRandomDomainMessage()),
            disabled: !state.Ready || state.Occupants > 0 || state.Points < 1 || state.GeneratedDomain != null,
            tooltip: Loc.GetString("bitrunning-console-random-tooltip")));

        var points = Tgui.HBox(4, BitrunningTgui.Icon("star", Pink), Tgui.Text(state.Points.ToString()));
        points.ToolTip = Loc.GetString("bitrunning-console-points-tooltip");
        points.MouseFilter = MouseFilterMode.Stop;
        points.VerticalAlignment = VAlignment.Center;
        _domainButtons.AddChild(points);
    }

    private void FillDomains(QuantumConsoleState state)
    {
        _domainList.RemoveAllChildren();
        _domainList.AddChild(Tabs());
        foreach (var domain in state.Domains.OrderBy(d => d.Cost).Where(d => d.Difficulty == _tab))
        {
            _domainList.AddChild(DomainEntry(state, domain));
        }
    }

    private Control Tabs()
    {
        var tabs = Tgui.HBox(2);
        (BitrunningDifficulty Difficulty, string Name, Color Text, string? Trailing)[] list =
        {
            (BitrunningDifficulty.None, "bitrunning-console-tab-peaceful", Color.White, null),
            (BitrunningDifficulty.Low, "bitrunning-console-tab-easy", Color.Black, null),
            (BitrunningDifficulty.Medium, "bitrunning-console-tab-medium", Color.White, null),
            (BitrunningDifficulty.High, "bitrunning-console-tab-hard", Color.White, "skull"),
        };

        foreach (var (difficulty, name, text, trailing) in list)
        {
            var tab = new BitrunningColoredTab(Loc.GetString(name), "chevron-down", DifficultyColor(difficulty), text,
                _tab == difficulty, trailing);
            tab.OnPressed += _ =>
            {
                _tab = difficulty;
                if (_state != null)
                    FillDomains(_state);
            };
            tabs.AddChild(tab);
        }

        return tabs;
    }

    /// <summary>DomainEntry: Collapsible цвета сложности, кнопка загрузки справа.</summary>
    private Control DomainEntry(QuantumConsoleState state, QuantumConsoleDomainEntry domain)
    {
        var current = state.GeneratedDomain == domain.Id;
        var occupied = state.Occupants > 0;
        var canView = domain.Name != Loc.GetString("bitrunning-console-redacted");
        var open = _expanded.Contains(domain.Id);

        string? loadIcon;
        string loadName;
        if (state.Randomized)
        {
            loadIcon = null;
            loadName = "???";
        }
        else if (current)
        {
            loadIcon = "download";
            loadName = Loc.GetString("bitrunning-console-loaded-button");
        }
        else
        {
            loadIcon = "coins";
            loadName = Loc.GetString("bitrunning-console-load");
        }

        var header = BitrunningTgui.Button(domain.Name, open ? "chevron-down" : "chevron-right", DifficultyColor(domain.Difficulty),
            () =>
            {
                if (!_expanded.Remove(domain.Id))
                    _expanded.Add(domain.Id);
                if (_state != null)
                    FillDomains(_state);
            }, fluid: true);
        header.TextLabel.HorizontalAlignment = HAlignment.Left;
        if (domain.Difficulty == BitrunningDifficulty.Low)
            header.TextLabel.FontColorOverride = Color.Black;

        if (canView && header.TextLabel.Parent is { } title)
        {
            if (domain.Modular)
                title.AddChild(TitleIcon("cubes", domain.Difficulty));
            if (domain.Secondary)
                title.AddChild(TitleIcon("gem", domain.Difficulty));
            if (domain.Ghosts)
                title.AddChild(TitleIcon("ghost", domain.Difficulty));
        }

        var id = domain.Id;
        var load = BitrunningTgui.Button(loadName, loadIcon,
            onPressed: () => OnMessage?.Invoke(new QuantumConsoleSetDomainMessage(id)),
            disabled: state.GeneratedDomain != null || !state.Ready || occupied || state.Points < domain.Cost,
            tooltip: state.GeneratedDomain != null ? Loc.GetString("bitrunning-console-stop-first") : null);

        var row = Tgui.HBox(4, header, load);
        var box = Tgui.VBox(4, row);
        if (!open)
            return box;

        var desc = domain.Description;
        if (domain.Modular)
            desc += Loc.GetString("bitrunning-console-modular-suffix");
        if (domain.Secondary)
            desc += Loc.GetString("bitrunning-console-secondary-suffix");
        if (domain.Ghosts)
            desc += Loc.GetString("bitrunning-console-ghosts-suffix");

        var descLabel = Tgui.Paragraph(desc, Tgui.Label);
        descLabel.HorizontalExpand = true;
        descLabel.SizeFlagsStretchRatio = 4;

        var details = Tgui.VBox(4,
            WithTooltip(DisplayDetails(domain.Cost.ToString(), Pink, "star"), "bitrunning-console-cost-tooltip"),
            WithTooltip(DisplayDetails(domain.Reward, Gold, "coins"), "bitrunning-console-reward-tooltip"));
        details.HorizontalExpand = true;
        details.SizeFlagsStretchRatio = 1;

        var body = Tgui.HBox(0, descLabel, BitrunningTgui.VDivider(), details);
        body.MinHeight = 60;
        body.Margin = new Thickness(4, 2, 4, 6);
        box.AddChild(body);
        return box;
    }

    private static Control TitleIcon(string icon, BitrunningDifficulty difficulty)
    {
        var label = BitrunningTgui.Icon(icon, difficulty == BitrunningDifficulty.Low ? Color.Black : Tgui.TextColor);
        label.Margin = new Thickness(6, 0, 0, 0);
        return label;
    }

    private static Control WithTooltip(Control control, string tooltip)
    {
        control.ToolTip = Loc.GetString(tooltip);
        control.MouseFilter = MouseFilterMode.Stop;
        return control;
    }

    /// <summary>DisplayDetails: «Никакие», текст, либо до четырёх иконок, либо «число + иконка».</summary>
    private static Control DisplayDetails(string amount, Color color, string icon)
    {
        if (!int.TryParse(amount, out var count))
            return Tgui.Text(amount, Tgui.Label);

        if (count == 0)
            return Tgui.Text(Loc.GetString("bitrunning-console-none"), Tgui.Label);

        if (count > 4)
            return Tgui.HBox(4, Tgui.Text(count.ToString()), BitrunningTgui.Icon(icon, color));

        var box = Tgui.HBox(4);
        for (var i = 0; i < count; i++)
        {
            box.AddChild(BitrunningTgui.Icon(icon, color));
        }

        return box;
    }

    /// <summary>AvatarDisplay: «пилот как "аватар"», значки урона и здоровье от -100 до 100.</summary>
    private void FillAvatars(QuantumConsoleState state)
    {
        _avatarButtons.RemoveAllChildren();
        if (state.GeneratedDomain != null)
        {
            var retries = Tgui.HBox(4, BitrunningTgui.Icon("broadcast-tower", Tgui.Green), Tgui.Text(state.RetriesLeft.ToString()));
            retries.ToolTip = Loc.GetString("bitrunning-console-retries-tooltip");
            retries.MouseFilter = MouseFilterMode.Stop;
            retries.VerticalAlignment = VAlignment.Center;
            _avatarButtons.AddChild(retries);
        }

        _avatarButtons.AddChild(BitrunningTgui.Button(Loc.GetString("bitrunning-console-refresh"), "sync",
            onPressed: () => OnMessage?.Invoke(new QuantumConsoleRefreshMessage()),
            tooltip: Loc.GetString("bitrunning-console-refresh-tooltip")));

        _avatarTable.RemoveAllChildren();
        foreach (var avatar in state.Avatars)
        {
            var who = new RichTextLabel { HorizontalExpand = true, VerticalAlignment = VAlignment.Center };
            var message = new Robust.Shared.Utility.FormattedMessage();
            message.PushColor(Tgui.Label);
            message.AddText(Loc.GetString("bitrunning-console-avatar-as", ("pilot", avatar.Pilot)) + " ");
            message.Pop();
            message.PushColor(Color.White);
            message.AddText($"\"{avatar.Name}\"");
            message.Pop();
            who.SetMessage(message);

            var icons = Tgui.HBox(4);
            if (avatar.Brute == 0 && avatar.Burn == 0 && avatar.Toxin == 0 && avatar.Airloss == 0)
                icons.AddChild(BitrunningTgui.Icon("check", Tgui.Green));
            icons.AddChild(BitrunningTgui.Icon("tint", avatar.Brute > 50 ? Tgui.Bad : Tgui.Grey));
            icons.AddChild(BitrunningTgui.Icon("fire", avatar.Burn > 50 ? Tgui.Average : Tgui.Grey));
            icons.AddChild(BitrunningTgui.Icon("skull-crossbones", avatar.Toxin > 50 ? Tgui.Green : Tgui.Grey));
            icons.AddChild(BitrunningTgui.Icon("lungs", avatar.Airloss > 50 ? Tgui.Blue : Tgui.Grey));
            icons.Margin = new Thickness(6, 0);

            var health = avatar.Health;
            var color = health >= 90 ? Tgui.Good : health >= 50 ? Tgui.Average : Tgui.Bad;
            var ratio = (health + 100f) / 200f;
            var bar = Tgui.ProgressBar(ratio, color, $"{MathF.Round(ratio * 100)}%");
            bar.HorizontalExpand = true;

            _avatarTable.AddChild(Tgui.HBox(4, who, icons, bar));
        }
    }

    private void FillFooter(QuantumConsoleState state)
    {
        _footer.RemoveAllChildren();
        string selected;
        if (state.GeneratedDomain is { } generated)
        {
            selected = state.Randomized
                ? "???"
                : state.Domains.Find(d => d.Id == generated)?.Name ?? generated;
        }
        else
        {
            selected = Loc.GetString("bitrunning-console-none-loaded");
        }

        var notice = Tgui.NoticeBox(selected, state.GeneratedDomain != null ? Tgui.Blue : null);
        notice.HorizontalExpand = true;

        var stop = BitrunningTgui.Confirm(Loc.GetString("bitrunning-console-stop"),
            () => OnMessage?.Invoke(new QuantumConsoleStopDomainMessage()),
            disabled: !state.Ready || state.GeneratedDomain == null,
            tooltip: Loc.GetString("bitrunning-console-stop-tooltip"));

        _footer.AddChild(notice);
        _footer.AddChild(stop);
    }
}
