using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cadia;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cadia;

/// <summary>Окно ведущего. Каждая кнопка только просит сервер, а тот сам проверяет права.</summary>
public sealed class CadiaEventPanelWindow : DefaultWindow
{
    [Dependency] private readonly IEntityManager _entMan = default!;

    private readonly Label _status = new();
    private readonly Button _veilToggle = new() { HorizontalExpand = true };
    private readonly LineEdit _announce = new() { HorizontalExpand = true };
    private readonly LineEdit _filter = new() { HorizontalExpand = true };
    private readonly BoxContainer _players = new() { Orientation = BoxContainer.LayoutOrientation.Vertical, SeparationOverride = 2 };
    private readonly Button _closeImperium = new();
    private readonly Button _closeChaos = new();
    private CadiaPanelStateEvent? _state;
    private string _shown = string.Empty;

    public CadiaEventPanelWindow()
    {
        IoCManager.InjectDependencies(this);
        MinSize = SetSize = new Vector2(900, 720);
        Title = Loc.GetString("cadia-panel-title");

        var root = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Vertical, Margin = new Thickness(8), SeparationOverride = 6 };
        root.AddChild(_status);

        root.AddChild(Row("cadia-panel-war",
            ActionButton("cadia-panel-start", CadiaPanelAction.Start),
            ActionButton("cadia-panel-next-phase", CadiaPanelAction.NextPhase),
            ActionButton("cadia-panel-intro", CadiaPanelAction.ShowIntro),
            ActionButton("cadia-panel-fall", CadiaPanelAction.BeginFall)));
        root.AddChild(Row("cadia-panel-waves",
            ActionButton("cadia-panel-wave-imperium", CadiaPanelAction.WaveImperium),
            ActionButton("cadia-panel-wave-chaos", CadiaPanelAction.WaveChaos),
            _closeImperium,
            _closeChaos));
        _closeImperium.OnPressed += _ => Send(CadiaPanelAction.ToggleCloseImperium);
        _closeChaos.OnPressed += _ => Send(CadiaPanelAction.ToggleCloseChaos);
        root.AddChild(Row("cadia-panel-storms",
            ActionButton("cadia-panel-storm-rain", CadiaPanelAction.StormBloodRain),
            ActionButton("cadia-panel-storm-whispers", CadiaPanelAction.StormWhispers),
            ActionButton("cadia-panel-storm-surge", CadiaPanelAction.StormDaemonSurge),
            ActionButton("cadia-panel-warp", CadiaPanelAction.AddWarp),
            ActionButton("cadia-panel-weather", CadiaPanelAction.CycleWeather)));
        root.AddChild(Row("cadia-panel-veil",
            _veilToggle,
            ActionButton("cadia-panel-veil-add", CadiaPanelAction.VeilAddMinute),
            ActionButton("cadia-panel-veil-cut", CadiaPanelAction.VeilCutMinute),
            ActionButton("cadia-panel-drop-recharge", CadiaPanelAction.DropRecharge),
            ActionButton("cadia-panel-drop-pod", CadiaPanelAction.DropPod)));
        _veilToggle.OnPressed += _ => Send(CadiaPanelAction.VeilToggle);
        root.AddChild(Row("cadia-panel-win",
            ActionButton("cadia-panel-win-imperium", CadiaPanelAction.WinImperium),
            ActionButton("cadia-panel-win-chaos", CadiaPanelAction.WinChaos)));

        _announce.PlaceHolder = Loc.GetString("cadia-panel-announce-placeholder");
        var announce = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        announce.AddChild(_announce);
        announce.AddChild(AnnounceButton("cadia-panel-announce-all", CadiaPanelAction.AnnounceAll));
        announce.AddChild(AnnounceButton("cadia-panel-announce-imperium", CadiaPanelAction.AnnounceImperium));
        announce.AddChild(AnnounceButton("cadia-panel-announce-chaos", CadiaPanelAction.AnnounceChaos));
        root.AddChild(announce);

        _filter.PlaceHolder = Loc.GetString("cadia-panel-filter");
        _filter.OnTextChanged += _ => Rebuild();
        root.AddChild(_filter);

        var scroll = new ScrollContainer { VerticalExpand = true, HScrollEnabled = false };
        scroll.AddChild(_players);
        root.AddChild(scroll);
        Contents.AddChild(root);

        OnClose += () => _entMan.EntityNetManager.SendSystemNetworkMessage(new CadiaPanelSubscribeEvent(false));
        _entMan.EntityNetManager.SendSystemNetworkMessage(new CadiaPanelSubscribeEvent(true));
    }

    private void Send(CadiaPanelAction action, string text = "", Guid? target = null)
    {
        _entMan.EntityNetManager.SendSystemNetworkMessage(new CadiaPanelActionEvent(action, text, target));
    }

    private Button ActionButton(string key, CadiaPanelAction action)
    {
        var button = new Button { Text = Loc.GetString(key), HorizontalExpand = true };
        button.OnPressed += _ => Send(action);
        return button;
    }

    private Button AnnounceButton(string key, CadiaPanelAction action)
    {
        var button = new Button { Text = Loc.GetString(key) };
        button.OnPressed += _ =>
        {
            Send(action, _announce.Text);
            _announce.Text = string.Empty;
        };
        return button;
    }

    private static Control Row(string key, params Control[] buttons)
    {
        var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
        row.AddChild(new Label { Text = Loc.GetString(key), MinWidth = 110, FontColorOverride = Color.FromHex("#e8c66a") });
        foreach (var button in buttons)
        {
            button.HorizontalExpand = true;
            row.AddChild(button);
        }
        return row;
    }

    public void Update(CadiaPanelStateEvent state)
    {
        _state = state;
        _closeImperium.Text = Loc.GetString(state.ImperiumForced ? "cadia-panel-open-imperium" : "cadia-panel-close-imperium");
        _closeChaos.Text = Loc.GetString(state.ChaosForced ? "cadia-panel-open-chaos" : "cadia-panel-close-chaos");
        _veilToggle.Text = Loc.GetString(state.VeilUp ? "cadia-panel-veil-drop" : "cadia-panel-veil-raise");
        _status.Text = !state.Active
            ? Loc.GetString("cadia-panel-no-war")
            : Loc.GetString("cadia-panel-status",
                ("phase", Loc.GetString($"cadia-hud-phase-{state.Phase.ToString().ToLowerInvariant()}")),
                ("time", $"{state.SecondsLeft / 60}:{state.SecondsLeft % 60:00}"),
                ("alive", state.PylonsAlive), ("total", state.PylonsTotal), ("warp", state.Warp),
                ("imperium", state.ImperiumAlive), ("imperiumSeats", state.ImperiumSeats),
                ("chaos", state.ChaosAlive), ("chaosSeats", state.ChaosSeats), ("saved", state.Saved),
                ("veil", Loc.GetString(state.VeilUp ? "cadia-panel-veil-up" : "cadia-panel-veil-down")),
                ("refugees", state.RefugeesSaved), ("refugeesTotal", state.RefugeesTotal));
        Rebuild();
    }

    private void Rebuild()
    {
        if (_state == null)
            return;
        // Пересобирать только при изменениях: иначе кнопка исчезнет между нажатием и отпусканием.
        var filter = _filter.Text.Trim();
        var key = filter + string.Join(';', _state.Players.ConvertAll(p => $"{p.User}{p.Character}{p.Alive}{p.Banned}{p.Role}{p.TeamKills}{p.TeamDamage}"));
        if (key == _shown)
            return;
        _shown = key;

        _players.RemoveAllChildren();
        foreach (var player in _state.Players)
        {
            if (filter.Length > 0 &&
                !player.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                !player.Character.Contains(filter, StringComparison.OrdinalIgnoreCase))
                continue;

            var row = new BoxContainer { Orientation = BoxContainer.LayoutOrientation.Horizontal, SeparationOverride = 4 };
            var color = player.Faction == CadiaFaction.Imperium ? "#e8c66a" : "#d23b2a";
            row.AddChild(new Label
            {
                Text = $"{(player.Alive ? "●" : "✝")} {player.Character} ({player.Name}) — {player.Role}" +
                       (player.TeamKills > 0 || player.TeamDamage > 0 ? " " + Loc.GetString("cadia-panel-friendly-fire", ("kills", player.TeamKills), ("damage", player.TeamDamage)) : string.Empty),
                FontColorOverride = player.TeamKills > 0 ? Color.FromHex("#ff5a3c") : Color.FromHex(color),
                HorizontalExpand = true,
                ClipText = true,
            });
            var user = player.User;
            var remove = new Button { Text = Loc.GetString("cadia-panel-remove") };
            remove.OnPressed += _ => Send(CadiaPanelAction.RemovePlayer, target: user);
            var ban = new Button { Text = Loc.GetString("cadia-panel-ban") };
            ban.OnPressed += _ => Send(CadiaPanelAction.BanPlayer, target: user);
            row.AddChild(remove);
            row.AddChild(ban);
            _players.AddChild(row);
        }
    }
}
