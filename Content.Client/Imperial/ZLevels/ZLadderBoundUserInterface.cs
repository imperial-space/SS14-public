using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.ZLevels;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.ZLevels;

/// <summary>Радиальное меню лестницы: подняться или спуститься (как выбор у /obj/structure/ladder в SS13).</summary>
public sealed class ZLadderBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private static readonly SpriteSpecifier UpIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/ZLevels/ladder.rsi"), "ladder10");
    private static readonly SpriteSpecifier DownIcon = new SpriteSpecifier.Rsi(new ResPath("/Textures/Imperial/ZLevels/ladder.rsi"), "ladder01");

    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(new RadialMenuOptionBase[]
        {
            new RadialMenuActionOption<bool>(Choose, true)
            {
                ToolTip = Loc.GetString("zlevel-ladder-climb-up"),
                IconSpecifier = RadialMenuIconSpecifier.With(UpIcon),
            },
            new RadialMenuActionOption<bool>(Choose, false)
            {
                ToolTip = Loc.GetString("zlevel-ladder-climb-down"),
                IconSpecifier = RadialMenuIconSpecifier.With(DownIcon),
            },
        });
        _menu.OpenOverMouseScreenPosition();
    }

    private void Choose(bool up)
    {
        SendMessage(new ZLadderChoiceMessage(up));
        Close();
    }
}
