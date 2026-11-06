using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Chaplain;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Chaplain;

/// <summary>
/// Радиальное меню выбора обложки библии верховным жрецом (attack_self библии в SS13).
/// </summary>
[UsedImplicitly]
public sealed class ImperialBibleSkinBoundUserInterface : BoundUserInterface
{
    private static readonly ResPath BibleRsi = new("/Textures/Imperial/Chaplain/bible.rsi");

    private SimpleRadialMenu? _menu;

    public ImperialBibleSkinBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.Track(Owner);
        _menu.SetButtons(BuildOptions());
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildOptions()
    {
        foreach (var (state, _) in ImperialBibleSkins.All)
        {
            yield return new RadialMenuActionOption<string>(OnPicked, state)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new SpriteSpecifier.Rsi(BibleRsi, state)),
                ToolTip = Loc.GetString($"imperial-bible-skin-{state}"),
            };
        }
    }

    private void OnPicked(string skin)
    {
        SendMessage(new ImperialBibleSkinPickMessage(skin));
        Close();
    }
}
