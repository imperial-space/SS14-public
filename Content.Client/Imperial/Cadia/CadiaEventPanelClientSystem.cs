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

public sealed class CadiaEventPanelClientSystem : EntitySystem
{
    private CadiaEventPanelWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CadiaPanelOpenEvent>(OnOpen);
        SubscribeNetworkEvent<CadiaPanelStateEvent>(OnState);
    }

    private void OnOpen(CadiaPanelOpenEvent ev)
    {
        if (_window != null)
        {
            _window.MoveToFront();
            return;
        }
        _window = new CadiaEventPanelWindow();
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
    }

    private void OnState(CadiaPanelStateEvent ev)
    {
        _window?.Update(ev);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }
}
