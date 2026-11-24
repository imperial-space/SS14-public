// Imperial Weekly Mode Start
using Content.Client.Imperial.WeeklyMode.Cargo;
// Imperial Weekly Mode End
using Content.Client.Cargo.UI;
using Content.Shared.Cargo.BUI;
using JetBrains.Annotations;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Cargo.BUI;

[UsedImplicitly]
public sealed class CargoShuttleConsoleBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;

    [ViewVariables]
    // Imperial Weekly Mode: Original code:
    // private CargoShuttleMenu? _menu;
    // Imperial Weekly Mode Start
    private WeeklyCargoShuttleMenu? _menu;
    // Imperial Weekly Mode End

    public CargoShuttleConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        // Imperial Weekly Mode: Original code:
        // _menu = this.CreateWindow<CargoShuttleMenu>();
        // Imperial Weekly Mode Start
        _menu = this.CreateWindow<WeeklyCargoShuttleMenu>();
        // Imperial Weekly Mode End
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not CargoShuttleConsoleBoundUserInterfaceState cargoState) return;
        _menu?.SetAccountName(cargoState.AccountName);
        _menu?.SetShuttleName(cargoState.ShuttleName);
        _menu?.SetOrders(EntMan.System<SpriteSystem>(), _protoManager, cargoState.Orders);
    }
}
