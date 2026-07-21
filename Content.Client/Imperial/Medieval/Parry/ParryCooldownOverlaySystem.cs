using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.Player;
using Robust.Shared.Timing;
using Content.Shared.Hands.EntitySystems;

namespace Content.Client.Imperial.Medieval.MeleeParry;

public sealed class ParryCooldownOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlayMan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IInputManager _input = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    public override void Initialize()
    {
        base.Initialize();

        var overlay = new ParryCooldownOverlay(_timing, _input, EntityManager, _player, _hands);
        _overlayMan.AddOverlay(overlay);
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlayMan.RemoveOverlay<ParryCooldownOverlay>();
    }
}
