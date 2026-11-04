using System.Linq;
using System.Numerics;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Imperial.Kitchen;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Kitchen;

/// <summary>Открывает меню кулинарии и обменивается с сервером списком доступного.</summary>
public sealed class ImperialCraftMenuSystem : EntitySystem
{
    private ImperialCraftMenu? _window;

    public HashSet<string> Craftable = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ImperialCraftMenuActionEvent>(OnAction);
        SubscribeNetworkEvent<ImperialCraftStateMessage>(OnState);
    }

    private void OnAction(ImperialCraftMenuActionEvent args)
    {
        args.Handled = true;
        if (_window != null)
        {
            _window.Close();
            return;
        }

        _window = new ImperialCraftMenu(this);
        _window.OnClose += () => _window = null;
        _window.OpenCentered();
        Refresh();
    }

    public void Refresh() => RaiseNetworkEvent(new ImperialCraftStateRequest());

    public void Craft(string recipe) => RaiseNetworkEvent(new ImperialCraftMessage(recipe));

    private void OnState(ImperialCraftStateMessage msg)
    {
        Craftable = msg.Craftable;
        _window?.Rebuild();
    }
}
