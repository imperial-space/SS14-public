using Content.Client.Imperial.Lavaland.OrePoints.MiningVoucher.UI;
using Content.Shared.Imperial.Lavaland.OrePoints.MiningVoucher;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Lavaland.OrePoints.MiningVoucher;

public sealed class MiningVoucherBoundUserInterface : BoundUserInterface
{
    private MiningVoucherWindow? _window;

    public MiningVoucherBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<MiningVoucherWindow>();
        _window.OnKitSelected += OnKitSelected;
    }

    private void OnKitSelected(int kitIndex)
    {
        SendPredictedMessage(new MiningVoucherSelectKitMessage(kitIndex));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window == null)
            return;

        _window.OnKitSelected -= OnKitSelected;
        _window.OnClose -= Close;
        _window.Dispose();
    }
}
