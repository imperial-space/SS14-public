using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Клиентский BUI для окна Общения культа.
/// </summary>
public sealed class CultCommuneBui : BoundUserInterface
{
    [ViewVariables]
    private CultCommuneWindow? _window;

    public CultCommuneBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CultCommuneWindow>();

        _window.OnMessageSubmit += text =>
        {
            SendMessage(new CultCommuneTextMessage(text));
        };
    }
}
