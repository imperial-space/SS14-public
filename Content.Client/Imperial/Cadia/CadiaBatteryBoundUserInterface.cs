using System.Numerics;
using Content.Shared.Imperial.Cadia;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Input;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using Color = Robust.Shared.Maths.Color;

namespace Content.Client.Imperial.Cadia;

public sealed class CadiaBatteryBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private CadiaBatteryWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CadiaBatteryWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.OnTarget += target => SendMessage(new CadiaBatterySetTargetMessage(target));
        _window.OnFire += () => SendMessage(new CadiaBatteryFireMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CadiaBatteryBuiState battery)
            _window?.Update(battery);
    }
}
