using Content.Shared.Research;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Research.UI;

public sealed partial class MiniTechnologyCardControl
{
    public MiniTechnologyCardControl(WeeklyTechnologyData technology, IPrototypeManager prototypeManager, SpriteSystem spriteSys, FormattedMessage description)
    {
        RobustXamlLoader.Load(this);

        var discipline = prototypeManager.Index<TechDisciplinePrototype>(technology.Branch);
        Background.ModulateSelfOverride = discipline.Color;
        Texture.Texture = spriteSys.Frame0(technology.Icon);
        NameLabel.SetMessage(technology.Name);

        var tooltip = new Tooltip();
        tooltip.SetMessage(description);
        Main.TooltipSupplier = _ => tooltip;
    }
}
