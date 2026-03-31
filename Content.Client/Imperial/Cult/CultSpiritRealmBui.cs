using Content.Shared.Imperial.Cult;
using Robust.Client.UserInterface;

namespace Content.Client.Imperial.Cult;

/// <summary>
/// Клиентский BUI для выбора действия руны Царства духов.
/// </summary>
public sealed class CultSpiritRealmBui : BoundUserInterface
{
    public CultSpiritRealmBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        var window = this.CreateWindow<CultSpiritRealmWindow>();

        window.OnHomunculiSelected += () =>
        {
            SendMessage(new CultSpiritRealmChoiceMessage(true));
        };

        window.OnSpiritSelected += () =>
        {
            SendMessage(new CultSpiritRealmChoiceMessage(false));
        };
    }
}
