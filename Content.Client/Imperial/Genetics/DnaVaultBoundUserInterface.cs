using System.Numerics;
using Content.Shared.Imperial.Genetics;
using Content.Shared.Imperial.Genetics.Prototypes;
using JetBrains.Annotations;
using Content.Client.Imperial.UI;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Genetics;

[UsedImplicitly]
public sealed class DnaVaultBoundUserInterface : BoundUserInterface
{
    private DnaVaultWindow? _window;

    public DnaVaultBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DnaVaultWindow>();
        _window.OnChoose += mutation => SendMessage(new DnaVaultChooseMessage(mutation));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DnaVaultBoundUserInterfaceState vault)
            _window?.UpdateState(vault);
    }
}
