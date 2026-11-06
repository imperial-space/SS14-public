using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Fishing;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Fishing;

/// <summary>Радиальное меню выбора измерения генератора порталов (и отвязки мест ловли).</summary>
public sealed class ImperialFishingPortalBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly ResPath RadialRsi = new("/Textures/Imperial/Fishing/radial.rsi");

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.OpenOverMouseScreenPosition();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);
        if (state is not ImperialFishingPortalBoundUserInterfaceState portal || _menu == null)
            return;

        var buttons = new List<RadialMenuOptionBase>();
        foreach (var option in portal.Options)
        {
            buttons.Add(new RadialMenuActionOption<string>(key => SendMessage(new ImperialFishingPortalSelectMessage(key)), option.Key)
            {
                ToolTip = option.Name,
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(RadialRsi, option.RadialState)),
            });
        }

        _menu.SetButtons(buttons);
    }
}
