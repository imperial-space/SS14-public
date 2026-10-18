using Content.Shared.Imperial.Genetics;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Genetics;

[UsedImplicitly]
public sealed class DnaConsoleBoundUserInterface : BoundUserInterface
{
    private DnaConsoleWindow? _window;

    public DnaConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DnaConsoleWindow>();
        _window.OnMessage += SendMessage;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is DnaConsoleBoundUserInterfaceState dna)
            _window?.UpdateState(dna);
    }
}
