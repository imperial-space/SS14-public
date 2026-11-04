using System.Numerics;
using Content.Shared.Imperial.Disease;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Disease;

public sealed class PandemicBoundUserInterface : BoundUserInterface
{
    private PandemicWindow? _window;

    public PandemicBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PandemicWindow>();
        _window.EjectBeaker += () => SendMessage(new PandemicEjectBeakerMessage());
        _window.EmptyBeaker += () => SendMessage(new PandemicEmptyBeakerMessage());
        _window.CreateCulture += index => SendMessage(new PandemicCreateCultureMessage(index));
        _window.CreateVaccine += id => SendMessage(new PandemicCreateVaccineMessage(id));
        _window.RenameDisease += (index, name) => SendMessage(new PandemicRenameDiseaseMessage(index, name));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is PandemicBoundUserInterfaceState pandemicState)
            _window?.UpdateState(pandemicState);
    }
}
