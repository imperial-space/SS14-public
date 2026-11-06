using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

// ─── LaborClaimConsole ─────────────────────────────────────────────────────

public sealed class LaborClaimBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public LaborClaimBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("labor-claim-title"), new Vector2(360, 240), out _body);
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _window?.Dispose();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not LaborClaimState s || _body == null)
            return;

        _body.RemoveAllChildren();
        _body.AddChild(SecurityUi.Field(Loc.GetString("labor-claim-status"), s.StatusInfo));
        _body.AddChild(SecurityUi.Field(Loc.GetString("labor-claim-id-points"), s.IdPoints.ToString()));
        _body.AddChild(SecurityUi.Field(Loc.GetString("labor-claim-unclaimed"), s.UnclaimedPoints.ToString()));
        _body.AddChild(SecurityUi.Button(Loc.GetString("labor-claim-claim"),
            () => SendMessage(new LaborClaimActionMessage(LaborClaimAction.ClaimPoints)), s.UnclaimedPoints == 0));
        _body.AddChild(SecurityUi.Button(Loc.GetString("labor-claim-move"),
            () => SendMessage(new LaborClaimActionMessage(LaborClaimAction.MoveShuttle)), !s.CanGoHome));
    }
}
