using Content.Shared.Imperial.Research;
using Content.Shared.Research.Components;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Research;

/// <summary>Консоль исследований с окном tgui Techweb TG.</summary>
[UsedImplicitly]
public sealed class ImperialTgResearchConsoleBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ImperialTgResearchConsoleMenu? _menu;

    public ImperialTgResearchConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<ImperialTgResearchConsoleMenu>();
        _menu.SetEntity(Owner);
        _menu.OnResearch += id => SendMessage(new ImperialTgResearchNodeMessage(id));
        _menu.OnEnqueue += id => SendMessage(new ImperialTgEnqueueNodeMessage(id));
        _menu.OnDequeue += id => SendMessage(new ImperialTgDequeueNodeMessage(id));
        _menu.OnToggleLock += () => SendMessage(new ImperialTgToggleLockMessage());
        _menu.OnServerButton += () => SendMessage(new ConsoleServerSelectionMessage());

        _menu.UpdateState(State is ResearchConsoleBoundInterfaceState state ? state.Points : 0);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ResearchConsoleBoundInterfaceState cast)
            _menu?.UpdateState(cast.Points);
    }
}
