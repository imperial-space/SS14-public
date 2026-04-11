using Content.Client.Imperial.BSA.UI;
using Content.Shared.Imperial.BSA;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.BSA;

public sealed class BSABoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private BSAConsoleWindow? _window;

    public BSABoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BSAConsoleWindow>();

        _window.OnFire += () =>
        {
            SendMessage(new BSAFireMessage());
        };

        _window.OnSelectTarget += target =>
        {
            SendMessage(new BSASelectTargetMessage(target));
        };

        _window.OnScan += () =>
        {
            SendMessage(new BSAScanMessage());
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is BSAUIState bsaState)
            _window?.UpdateState(bsaState);
    }
}
