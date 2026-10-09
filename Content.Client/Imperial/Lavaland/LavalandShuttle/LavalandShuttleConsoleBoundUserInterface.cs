using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Lavaland.LavalandShuttle;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Lavaland.LavalandShuttle;

[UsedImplicitly]
public sealed class LavalandShuttleConsoleBoundUserInterface : BoundUserInterface
{
    private LavalandShuttleConsoleWindow? _window;

    public LavalandShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<LavalandShuttleConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is LavalandShuttleConsoleBoundUserInterfaceState s)
            _window?.UpdateState(s);
    }
}

/// <summary>Окно 1 в 1 с tgui ShuttleConsole из SS13 (350×230 + срочная отправка).</summary>
public sealed class LavalandShuttleConsoleWindow : DefaultWindow
{
    public event Action<BoundUserInterfaceMessage>? OnMessage;

    private readonly BoxContainer _root = Tgui.VBox(6);

    public LavalandShuttleConsoleWindow()
    {
        Title = Loc.GetString("lavaland-shuttle-ui-title");
        MinSize = SetSize = new Vector2(350, 270);
        _root.Margin = new Thickness(6);
        Contents.AddChild(Tgui.Window(_root, scrollable: false));
    }

    private static Color StatusColor(LavalandShuttleStatus status) => status switch
    {
        LavalandShuttleStatus.InTransit => Tgui.Good,
        LavalandShuttleStatus.Idle or LavalandShuttleStatus.Igniting or LavalandShuttleStatus.Recharging => Tgui.Average,
        _ => Tgui.Bad,
    };

    private static string DestinationName(LavalandShuttleDestination destination) => Loc.GetString(destination switch
    {
        LavalandShuttleDestination.Station => "lavaland-shuttle-location-station",
        LavalandShuttleDestination.Lavaland => "lavaland-shuttle-location-lavaland",
        _ => "lavaland-shuttle-ui-select-destination",
    });

    public void UpdateState(LavalandShuttleConsoleBoundUserInterfaceState state)
    {
        _root.RemoveAllChildren();

        // timer_str
        _root.AddChild(new Label
        {
            Text = state.TimerStr,
            FontOverride = Tgui.MonoFont(26),
            FontColorOverride = Tgui.TextColor,
            HorizontalAlignment = HAlignment.Center,
        });

        // STATUS:
        var status = Tgui.HBox(6,
            Tgui.Text(Loc.GetString("lavaland-shuttle-ui-status"), size: 14, bold: true),
            Tgui.Text(Loc.GetString("lavaland-shuttle-status-" + state.Status.ToString().ToLowerInvariant()), StatusColor(state.Status), 14));
        status.HorizontalAlignment = HAlignment.Center;
        _root.AddChild(status);

        // Destination
        Control destination;
        if (state.Locations.Count == 0)
        {
            destination = state.Destination != LavalandShuttleDestination.None
                ? Tgui.Text(DestinationName(state.Destination), Tgui.Average)
                : Tgui.Text(Loc.GetString("lavaland-shuttle-ui-not-available"), Tgui.Bad);
        }
        else if (state.Locations.Count == 1)
        {
            destination = Tgui.Text(DestinationName(state.Locations[0]), Tgui.Average);
        }
        else
        {
            var dropdown = new OptionButton { MinWidth = 200, Disabled = state.Locked };
            dropdown.AddItem(Loc.GetString("lavaland-shuttle-ui-select-destination"), 0);
            foreach (var location in state.Locations)
                dropdown.AddItem(DestinationName(location), (int) location);
            dropdown.SelectId((int) state.Destination);
            dropdown.OnItemSelected += args =>
            {
                if (args.Id != 0)
                    OnMessage?.Invoke(new LavalandShuttleSetDestinationMessage((LavalandShuttleDestination) args.Id));
            };
            destination = dropdown;
        }

        var list = Tgui.LabeledList(
            (Loc.GetString("lavaland-shuttle-ui-location"), Tgui.Text(state.Location)),
            (Loc.GetString("lavaland-shuttle-ui-destination"), destination));

        var hasDestination = state.Destination != LavalandShuttleDestination.None;
        var depart = Tgui.Button(Loc.GetString("lavaland-shuttle-ui-depart"),
            () => OnMessage?.Invoke(new LavalandShuttleDepartMessage()),
            disabled: !hasDestination || state.Locked, fluid: true, icon: "arrow-up");

        var expressText = state.ExpressCooldown > 0
            ? Loc.GetString("lavaland-shuttle-ui-express-cooldown", ("time", $"{state.ExpressCooldown / 60:00}:{state.ExpressCooldown % 60:00}"))
            : Loc.GetString("lavaland-shuttle-ui-express");
        var express = Tgui.Button(expressText,
            () => OnMessage?.Invoke(new LavalandShuttleExpressDepartMessage()),
            color: Tgui.Orange, disabled: !hasDestination || state.Locked || state.ExpressCooldown > 0,
            fluid: true, icon: "forward", tooltip: Loc.GetString("lavaland-shuttle-ui-express-tooltip"));

        var controls = Tgui.VBox(6, list, depart, express);
        _root.AddChild(Tgui.MakeSection(Loc.GetString("lavaland-shuttle-ui-controls"), null, controls));
    }
}
