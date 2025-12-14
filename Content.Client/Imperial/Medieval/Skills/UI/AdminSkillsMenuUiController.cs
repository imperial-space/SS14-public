using Content.Shared.Imperial.Medieval.Factions;
using Content.Shared.Imperial.Medieval.Factions.Components;
using Content.Shared.Imperial.Medieval.Factions.Prototypes;
using Content.Shared.Imperial.Medieval.Skills;
using JetBrains.Annotations;
using Robust.Client.Player;
using Robust.Client.UserInterface.Controllers;
using Robust.Shared.Prototypes;

namespace Content.Client.Imperial.Medieval.Skills.UI;

[UsedImplicitly]
public sealed class AdminSkillsMenuUiController : UIController
{
    [Dependency] private readonly IEntityManager _entityManager = default!;

    private AdminSkillsMenu? _menu;

    public void OpenMenu(NetEntity target, Dictionary<string, int> skills)
    {
        _menu?.Close();

        _menu = new AdminSkillsMenu(target, skills);
        _menu.OnClose += () => _menu = null;

        _menu.LevelSet += (skill, level) =>
        {
            _entityManager.RaisePredictiveEvent(new SetSkillLevelMessage(_menu.Target, skill, level));
        };

        _menu.OpenCentered();
    }
}
