using Content.Shared.Shuttles.BUIStates;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Content.Shared.Imperial.Medieval.CartographerTable;

namespace Content.Client.Imperial.Medieval.CartographerTable;

[UsedImplicitly]
public sealed class MedievalCartographerTableBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private MedievalCartographerTableWindow? _window;

    public MedievalCartographerTableBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<MedievalCartographerTableWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not MedievalCartographerBoundUserInterfaceState navState)
            return;

        _window?.UpdateState(navState);
    }
}
