using System;
using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Medieval.Ships.Sail;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.Ships.Sail;

[UsedImplicitly]
public sealed class SailMenuBoundUserInterface : BoundUserInterface
{
    private SimpleRadialMenu? _menu;

    public SailMenuBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons([
            new RadialMenuActionOption<SailMenuAction>(SendAction, SailMenuAction.RotateLeft)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/rotate_ccw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("sail-menu-rotate-left"),
            },
            new RadialMenuActionOption<SailMenuAction>(SendAction, SailMenuAction.ToggleFold)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/flip.svg.192dpi.png"))),
                ToolTip = Loc.GetString("sail-menu-toggle-fold"),
            },
            new RadialMenuActionOption<SailMenuAction>(SendAction, SailMenuAction.RotateRight)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(
                    new ResPath("/Textures/Interface/VerbIcons/rotate_cw.svg.192dpi.png"))),
                ToolTip = Loc.GetString("sail-menu-rotate-right"),
            },
        ]);
        _menu.OpenOverMouseScreenPosition();
    }

    private void SendAction(SailMenuAction action)
    {
        SendMessage(new SailMenuActionMessage(action));
    }
}
