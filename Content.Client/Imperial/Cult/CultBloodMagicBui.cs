using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Клиентский BUI для выбора заклинания кровавой магии.
/// </summary>
public sealed class CultBloodMagicBui : BoundUserInterface
{
    [ViewVariables]
    private CultBloodMagicSelectWindow? _selectWindow;

    [ViewVariables]
    private CultBloodMagicSwapWindow? _swapWindow;

    public CultBloodMagicBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _selectWindow = this.CreateWindow<CultBloodMagicSelectWindow>();

        _selectWindow.OnSpellSelected += spellId =>
        {
            SendMessage(new CultSpellSelectedMessage(spellId));
        };
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not CultBloodMagicSwapState swapState)
            return;

        // Закрываем предыдущее окно замены (если было открыто ранее)
        if (_swapWindow != null)
        {
            _swapWindow.Dispose();
            _swapWindow = null;
        }

        // Закрываем окно выбора заклинания перед открытием окна замены
        if (_selectWindow != null)
        {
            _selectWindow.Dispose();
            _selectWindow = null;
        }

        _swapWindow = this.CreateWindow<CultBloodMagicSwapWindow>();
        _swapWindow.Populate(swapState.NewSpellId, swapState.PreparedSpells);

        _swapWindow.OnSwapSelected += oldSpellId =>
        {
            SendMessage(new CultSpellSwapMessage(oldSpellId, swapState.NewSpellId));
        };
    }
}
