using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Клиентский BUI для выбора руны (открывается по Z при держании кинжала).
/// </summary>
public sealed class CultRuneSelectBui : BoundUserInterface
{
    [ViewVariables]
    private CultRuneSelectWindow? _window;

    public CultRuneSelectBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CultRuneSelectWindow>();

        _window.OnRuneSelected += (runeId, label) =>
        {
            SendMessage(new CultSelectRuneMessage(runeId, label));
        };
    }
}
