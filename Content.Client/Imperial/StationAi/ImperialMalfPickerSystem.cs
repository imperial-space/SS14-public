using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.StationAi;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.StationAi;

/// <summary>Меню модулей сбойного ИИ: открывается и обновляется событием с сервера.</summary>
public sealed class ImperialMalfPickerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private ImperialMalfPickerWindow? _window;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<ImperialMalfPickerEvent>(OnPicker);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _window?.Close();
    }

    private void OnPicker(ImperialMalfPickerEvent ev)
    {
        if (_window == null)
        {
            if (!ev.Open)
                return;

            _window = new ImperialMalfPickerWindow(_proto, _sprite);
            _window.OnBuy += id => RaiseNetworkEvent(new ImperialMalfBuyEvent(id));
            _window.OnClose += () => _window = null;
            _window.OpenCentered();
        }

        _window.Update(ev);
    }
}
