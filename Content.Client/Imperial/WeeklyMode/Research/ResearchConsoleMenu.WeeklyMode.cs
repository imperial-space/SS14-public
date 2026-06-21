using System.Linq;
using Content.Shared.Research;
using Content.Shared.Research.Components;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Research.UI;

public sealed partial class ResearchConsoleMenu
{
    private bool TryUpdateWeeklyPanels(ResearchConsoleBoundInterfaceState state, TechnologyDatabaseComponent database, bool hasAccess)
    {
        if (!database.WeeklyModeOnly)
            return false;

        var availableWeeklyTech = _research.GetAvailableWeeklyTechnologies(Entity);
        SyncWeeklyTechnologyList(AvailableCardsContainer, availableWeeklyTech);

        // i can't figure out the spacing so here you go
        TechnologyCardsContainer.AddChild(new Control
        {
            MinHeight = 10
        });

        foreach (var techId in database.CurrentTechnologyCards)
        {
            var tech = database.WeeklyTechnologies.FirstOrDefault(x => x.TechnologyId == techId);
            if (string.IsNullOrEmpty(tech.TechnologyId))
                continue;

            var cardControl = new TechnologyCardControl(tech, _prototype, _sprite, _research.GetWeeklyTechnologyDescription(tech, includeTier: false), state.Points, hasAccess);
            cardControl.OnPressed += () => OnTechnologyCardPressed?.Invoke(tech.TechnologyId);
            TechnologyCardsContainer.AddChild(cardControl);
        }

        var unlockedWeeklyTech = database.WeeklyTechnologies
            .Where(x => database.WeeklyUnlockedTechnologies.Any(id => id == x.TechnologyId));
        SyncWeeklyTechnologyList(UnlockedCardsContainer, unlockedWeeklyTech);
        return true;
    }

    private void SyncWeeklyTechnologyList(BoxContainer container, IEnumerable<WeeklyTechnologyData> technologies)
    {
        container.Children.Clear();
        foreach (var tech in technologies)
        {
            var mini = new MiniTechnologyCardControl(tech, _prototype, _sprite, _research.GetWeeklyTechnologyDescription(tech));
            container.AddChild(mini);
        }
    }
}
