using Content.Client.Imperial.Lavaland.OrePoints.UI;
using Content.Shared.Imperial.Lavaland.OrePoints;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Lavaland.OrePoints;

public sealed class OrePointsShopBoundUserInterface : BoundUserInterface
{
    private OrePointsShopWindow? _window;

    public OrePointsShopBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<OrePointsShopWindow>();
        _window.OnBuyPressed += OnBuyPressed;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not OrePointsShopUiState uiState)
            return;

        _window?.Populate(uiState.Entries, uiState.PlayerBalance);
    }

    private void OnBuyPressed(string itemId)
    {
        SendPredictedMessage(new OrePointsShopBuyMessage(itemId));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window == null)
            return;

        _window.OnBuyPressed -= OnBuyPressed;
        _window.OnClose -= Close;
        _window.Dispose();
    }
}
