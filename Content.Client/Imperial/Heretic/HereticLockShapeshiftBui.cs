using Content.Client.UserInterface.Controls;
using Content.Shared.Imperial.Heretic;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Heretic;

[UsedImplicitly]
public sealed class HereticLockShapeshiftBui : BoundUserInterface
{
    private static readonly ResPath EldritchMobsRsi = new("/Textures/Imperial/heretic/eldritch_mobs.rsi");

    private SimpleRadialMenu? _menu;

    public HereticLockShapeshiftBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();
        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(BuildButtons());
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> BuildButtons()
    {
        return new RadialMenuOptionBase[]
        {
            MakeButton(HereticLockShapeshiftCreature.RustWalker,
                new SpriteSpecifier.Rsi(EldritchMobsRsi, "rust_walker_s"),
                Loc.GetString("heretic-lock-shapeshift-rust-walker")),

            MakeButton(HereticLockShapeshiftCreature.AshOrb,
                new SpriteSpecifier.Rsi(EldritchMobsRsi, "ash_walker"),
                Loc.GetString("heretic-lock-shapeshift-ash-orb")),

            MakeButton(HereticLockShapeshiftCreature.FleshWorm,
                new SpriteSpecifier.Rsi(EldritchMobsRsi, "stalker"),
                Loc.GetString("heretic-lock-shapeshift-stalker")),

            MakeButton(HereticLockShapeshiftCreature.RawProphet,
                new SpriteSpecifier.Rsi(EldritchMobsRsi, "raw_prophet"),
                Loc.GetString("heretic-lock-shapeshift-raw-prophet")),
        };
    }

    private RadialMenuActionOption<HereticLockShapeshiftCreature> MakeButton(
        HereticLockShapeshiftCreature creature,
        SpriteSpecifier icon,
        string tooltip)
    {
        return new RadialMenuActionOption<HereticLockShapeshiftCreature>(OnCreatureSelected, creature)
        {
            IconSpecifier = RadialMenuIconSpecifier.With(icon),
            ToolTip = tooltip,
        };
    }

    private void OnCreatureSelected(HereticLockShapeshiftCreature creature)
    {
        SendMessage(new HereticLockShapeshiftSelectMessage(creature));
    }
}
