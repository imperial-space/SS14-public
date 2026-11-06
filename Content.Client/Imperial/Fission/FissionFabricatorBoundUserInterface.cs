using Robust.Shared.Utility;
using System.Globalization;
using System.Linq;
using System.Numerics;
using Content.Client.Imperial.UI;
using Content.Shared.Imperial.Fission;
using JetBrains.Annotations;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;

namespace Content.Client.Imperial.Fission;

[UsedImplicitly]
public sealed class FissionFabricatorBoundUserInterface : BoundUserInterface
{
    private FissionFabricatorWindow? _window;

    public FissionFabricatorBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<FissionFabricatorWindow>();
        _window.OnFabricate += id => SendMessage(new FissionFabricateMessage(id));
        _window.OnEject += (id, sheets) => SendMessage(new FissionEjectMaterialMessage(id, sheets));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is FissionFabricatorUiState fabricator)
            _window?.UpdateState(fabricator);
    }
}
