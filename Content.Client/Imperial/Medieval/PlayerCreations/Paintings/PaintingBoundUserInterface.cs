using Content.Shared.Imperial.Medieval.PlayerCreations.Paintings;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Medieval.PlayerCreations.Paintings;

public sealed class PaintingBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IClyde _clyde = default!;

    private PaintingWindow? _window;

    public PaintingBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaintingWindow>();
        _window.Open();
        _window.OnClose += Close;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not PaintingBoundUserInterfaceState cast || _window == null)
            return;

        var texture = PaintingHelper.GetTextureFromColorArray(_clyde, cast.Texture);

        _window.Populate(texture);
    }
}
