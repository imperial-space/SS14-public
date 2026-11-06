using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Changeling;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Changeling;

/// <summary>select_dna(): радиальное меню профилей и «Сбросить маскировку из плоти».</summary>
[UsedImplicitly]
public sealed class ImperialChangelingTransformBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private SimpleRadialMenu? _menu;

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        Update();
        _menu.OpenOverMouseScreenPosition();
    }

    public override void Update()
    {
        if (_menu == null || !EntMan.TryGetComponent<ImperialChangelingComponent>(Owner, out var ling))
            return;

        var buttons = new List<RadialMenuOptionBase>();
        // Для трансформирующего жала выбирается только ДНК.
        if (!ling.SelectingStingDna && !ling.SelectingHumanForm)
        {
            buttons.Add(new RadialMenuActionOption<int>(_ => SendMessage(new ImperialChangelingDropFleshMessage()), 0)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/delete.svg.192dpi.png"))),
                ToolTip = Loc.GetString("imperial-changeling-transform-drop-flesh"),
            });
        }

        foreach (var profile in ling.Profiles)
        {
            if (!EntMan.EntityExists(profile))
                continue;

            buttons.Add(new RadialMenuActionOption<NetEntity>(p => SendMessage(new ImperialChangelingTransformSelectMessage(p)), EntMan.GetNetEntity(profile))
            {
                IconSpecifier = RadialMenuIconSpecifier.With(profile),
                ToolTip = EntMan.GetComponent<MetaDataComponent>(profile).EntityName,
            });
        }

        _menu.SetButtons(buttons);
    }
}
