using Content.Shared.Research;
using Content.Shared.Research.Prototypes;
using Robust.Client.GameObjects;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Research.UI;

public sealed partial class TechnologyCardControl
{
    public TechnologyCardControl(WeeklyTechnologyData technology, IPrototypeManager prototypeManager, SpriteSystem spriteSys, FormattedMessage description, int points, bool hasAccess)
    {
        RobustXamlLoader.Load(this);

        var discipline = prototypeManager.Index<TechDisciplinePrototype>(technology.Branch);
        Background.ModulateSelfOverride = discipline.Color;

        DisciplineTexture.Texture = spriteSys.Frame0(discipline.Icon);
        TechnologyNameLabel.Text = technology.Name;
        var message = new FormattedMessage();
        message.AddMarkupOrThrow(Loc.GetString("research-console-tier-discipline-info",
            ("tier", technology.Tier), ("color", discipline.Color), ("discipline", Loc.GetString(discipline.Name))));
        TierLabel.SetMessage(message);
        UnlocksLabel.SetMessage(description);

        TechnologyTexture.Texture = spriteSys.Frame0(technology.Icon);

        if (!hasAccess)
            ResearchButton.ToolTip = Loc.GetString("research-console-no-access-popup");

        ResearchButton.Disabled = points < technology.Cost || !hasAccess;
        ResearchButton.OnPressed += _ => OnPressed?.Invoke();
    }
}
