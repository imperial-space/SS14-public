using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.ImperialStore;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Factions.UI;


public sealed class WantedDeskBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private WantedDeskMenu? _menu;

    public WantedDeskBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<WantedDeskMenu>();
        _menu.Owner = Owner;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is WantedDeskBoundUserInterfaceState cast)
            _menu?.Populate(cast);
    }
}
