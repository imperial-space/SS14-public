using System.Linq;
using System.Numerics;
using Content.Shared.Imperial.Security;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Security;

// ─── GulagItemReclaimer ────────────────────────────────────────────────────

public sealed class GulagReclaimerBoundUserInterface : BoundUserInterface
{
    private DefaultWindow? _window;
    private BoxContainer? _body;

    public GulagReclaimerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = SecurityUi.Window(Loc.GetString("gulag-reclaimer-title"), new Vector2(340, 300), out _body);
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
        if (state is not GulagReclaimerState s || _body == null)
            return;

        _body.RemoveAllChildren();
        if (s.Mobs.Count == 0)
        {
            _body.AddChild(new Label { Text = Loc.GetString("gulag-reclaimer-empty") });
            return;
        }

        for (var i = 0; i < s.Mobs.Count; i++)
        {
            var mob = s.Mobs[i];
            var row = SecurityUi.Row();
            row.AddChild(new Label { Text = s.Names[i], HorizontalExpand = true, ClipText = true });
            row.AddChild(SecurityUi.Button(Loc.GetString("gulag-reclaimer-release"),
                () => SendMessage(new GulagReclaimerReleaseMessage(mob)), !s.CanReclaim));
            _body.AddChild(row);
        }
    }
}
