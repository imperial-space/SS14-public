using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.StationAi;

/// <summary>Окна ИИ: списки камер и целей, тревоги станции, оглашение законов.</summary>
public sealed class ImperialStationAiUiSystem : EntitySystem
{
    private DefaultWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialAiListEvent>(OnList);
        SubscribeNetworkEvent<ImperialAiAlertsEvent>(OnAlerts);
        SubscribeNetworkEvent<ImperialAiLawsEvent>(OnLaws);
        SubscribeNetworkEvent<ImperialAiBorgListEvent>(OnBorgs);
        SubscribeNetworkEvent<ImperialAiBotListEvent>(OnBots);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    private void Show(DefaultWindow window)
    {
        _window?.Close();
        _window = window;
        window.OnClose += () =>
        {
            if (_window == window)
                _window = null;
        };
        window.OpenCentered();
    }

    private void OnList(ImperialAiListEvent ev)
    {
        var title = Loc.GetString(ev.Kind == ImperialAiListKind.Cameras ? "imperial-ai-list-cameras" : "imperial-ai-list-track");
        var window = new ImperialAiListWindow(title, ev.Entries);
        window.OnChosen += entity =>
        {
            RaiseNetworkEvent(new ImperialAiListChoiceEvent(ev.Kind, entity));
            window.Close();
        };
        Show(window);
    }

    private void OnAlerts(ImperialAiAlertsEvent ev)
    {
        var window = new ImperialAiAlertsWindow(ev.Alarms);
        window.OnJump += entity => RaiseNetworkEvent(new ImperialAiJumpEvent(entity));
        Show(window);
    }

    private void OnBots(ImperialAiBotListEvent ev)
    {
        var window = new ImperialAiBotsWindow(ev.Bots);
        window.OnCommand += (bot, command) =>
        {
            RaiseNetworkEvent(new ImperialAiBotCommandEvent(bot, command));
            window.Close();
        };
        Show(window);
    }

    private void OnBorgs(ImperialAiBorgListEvent ev)
    {
        var window = new ImperialAiBorgsWindow(ev.Borgs);
        window.OnCommand += (borg, command) =>
        {
            RaiseNetworkEvent(new ImperialAiBorgCommandEvent(borg, command));
            window.Close();
        };
        Show(window);
    }

    private void OnLaws(ImperialAiLawsEvent ev)
    {
        var window = new ImperialAiLawsWindow(ev.Laws, ev.Channels);
        window.OnState += (laws, channel) =>
        {
            RaiseNetworkEvent(new ImperialAiStateLawsEvent(laws, channel));
            window.Close();
        };
        Show(window);
    }
}
