using Content.Shared.Imperial.Medieval.Afk;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Input;

namespace Content.Client.Imperial.Medieval.Afk;

public sealed class MedievalAfkSystem : EntitySystem
{
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        _inputManager.UIKeyBindStateChanged += OnUIKeyBindStateChanged;
    }

    private bool OnUIKeyBindStateChanged(BoundKeyEventArgs args)
    {
        if (_playerManager.LocalSession == null)
            return false;

        var id = _playerManager.LocalSession.UserId;

        var ev = new MedievalPlayerActionEvent(id);
        RaiseNetworkEvent(ev);

        return false;
    }
}
