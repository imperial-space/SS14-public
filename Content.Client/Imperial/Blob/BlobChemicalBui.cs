using System;
using Content.Shared.Imperial.Blob;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Blob;

[UsedImplicitly]
public sealed class BlobChemicalBui : BoundUserInterface
{
    [ViewVariables]
    private BlobChemicalWindow? _window;

    public BlobChemicalBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BlobChemicalWindow>();
        _window.OnChemicalSelected += chemical => SendMessage(new BlobSelectChemicalMessage(chemical));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BlobChemicalMenuState chemicalState)
            _window?.SetState(chemicalState);
    }
}