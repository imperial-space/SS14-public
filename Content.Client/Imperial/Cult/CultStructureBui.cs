using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Клиентский BUI для алтаря, кузницы и архива культа.
/// Показывает список предметов которые можно создать.
/// </summary>
public sealed class CultStructureBui : BoundUserInterface
{
    [ViewVariables]
    private CultStructureWindow? _window;

    public CultStructureBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CultStructureWindow>();
        _window.OnItemSelected += itemId => SendMessage(new CultStructureCreateMessage(itemId));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is CultStructureBuiState s)
            _window?.SetItems(s.Items);
    }
}
