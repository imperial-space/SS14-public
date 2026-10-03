using Content.Client.Imperial.Lavaland.LavalandShuttle.UI;
using Content.Shared.Imperial.Lavaland.LavalandShuttle;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Lavaland.LavalandShuttle;

public sealed class LavalandShuttleConsoleBoundUserInterface : BoundUserInterface
{
    private LavalandShuttleConsoleWindow? _window;

    public LavalandShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindowCenteredLeft<LavalandShuttleConsoleWindow>();
        _window.OnSelectStation += OnSelectStation;
        _window.OnSelectLavaland += OnSelectLavaland;
        _window.OnDepart += OnDepart;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not LavalandShuttleConsoleBoundUserInterfaceState s)
            return;

        _window?.UpdateState(s);
    }

    private void OnSelectStation()
    {
        SendPredictedMessage(new LavalandShuttleFlyToStationMessage());
    }

    private void OnSelectLavaland()
    {
        SendPredictedMessage(new LavalandShuttleFlyToLavalandMessage());
    }

    private void OnDepart()
    {
        SendPredictedMessage(new LavalandShuttleDepartMessage());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing)
            return;

        if (_window == null)
            return;

        _window.OnSelectStation -= OnSelectStation;
        _window.OnSelectLavaland -= OnSelectLavaland;
        _window.OnDepart -= OnDepart;
        _window.OnClose -= Close;
        _window.Dispose();
    }
}
