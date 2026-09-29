using Content.Shared.Imperial.Heretic;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Heretic;

/// <summary>
/// Интерфейс Книги Мансуса. Анимацию самой книги ведёт сервер через Appearance.
/// </summary>
public sealed class HereticInfoBui : BoundUserInterface
{
    private HereticInfoWindow? _window;

    public HereticInfoBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        EnsureWindow();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not HereticInfoBuiState infoState)
            return;

        EnsureWindow();
        _window?.Populate(infoState);
    }

    private void EnsureWindow()
    {
        if (_window != null)
            return;

        _window = this.CreateWindow<HereticInfoWindow>();
        _window.OnPathSelected += id => SendMessage(new HereticSelectPathMessage { KnowledgeId = id });
        _window.OnKnowledgeSelected += id => SendMessage(new HereticResearchKnowledgeMessage { KnowledgeId = id });
        _window.OnDenyAscension += () => SendMessage(new HereticDenyAscensionMessage());
    }
}
