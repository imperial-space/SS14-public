using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.PlayerCreations.Paintings;

public sealed class EaselBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    private EaselWindow? _window;

    public EaselBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<EaselWindow>();
        _window.Open();
        _window.Canvas.OnClickUp += SendSave;
        _window.OnClose += Close;
        _window.OnSend += SendPainting;
    }

    private void SendSave()
    {
        if (_window == null)
            return;

        SendMessage(new EaselSaveMessage(_window.CanvasTexture));
    }

    private void SendPainting(string name, string description, string author)
    {
        if (_window == null)
            return;

        var localSession = _playerManager.LocalSession;
        if (localSession == null)
            return;

        SendMessage(new EaselSendPaintingMessage(_window.CanvasTexture, name, description, author, localSession.UserId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not EaselBoundUserInterfaceState cast || _window == null)
            return;

        _window.Populate(cast);
    }
}
