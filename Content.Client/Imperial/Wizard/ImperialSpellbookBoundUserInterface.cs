using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Actions.Components;
using Content.Shared.Imperial.Wizard;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Client.Imperial.Wizard;

public sealed class ImperialSpellbookBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private ImperialSpellbookWindow? _window;

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<ImperialSpellbookWindow>();
        _window.OnPurchase += id => SendMessage(new ImperialSpellbookPurchaseMessage(id));
        _window.OnRefund += id => SendMessage(new ImperialSpellbookRefundMessage(id));
        _window.OnRandomize += full => SendMessage(new ImperialSpellbookRandomizeMessage(full));
        _window.OnLoadout += id => SendMessage(new ImperialSpellbookLoadoutMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is ImperialSpellbookState book)
            _window?.Update(book);
    }
}
