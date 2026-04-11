using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Клиентский BUI для выбора руны телепортации.
/// </summary>
public sealed class CultTeleportBui : BoundUserInterface
{
    [ViewVariables]
    private CultTeleportSelectWindow? _window;

    public CultTeleportBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CultTeleportSelectWindow>();

        _window.OnRuneSelected += runeEntity =>
        {
            SendMessage(new CultTeleportSelectMessage(runeEntity));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CultTeleportBuiState teleportState && _window != null)
        {
            _window.PopulateRunes(teleportState.Runes);
        }
    }
}
