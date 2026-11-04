using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Bitrunning;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Bitrunning;

/// <summary>Действие «Информация о домене» открывает окно AvatarHelp.</summary>
public sealed class BitrunningAvatarHelpSystem : EntitySystem
{
    private AvatarHelpWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<BitrunningAvatarHelpEvent>(OnHelp);
    }

    private void OnHelp(BitrunningAvatarHelpEvent ev)
    {
        _window?.Close();
        _window = new AvatarHelpWindow(ev.Help);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}
