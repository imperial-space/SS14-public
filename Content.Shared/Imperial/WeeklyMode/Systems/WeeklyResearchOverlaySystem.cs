using System.Linq;
using Content.Shared.Research.Components;
using Content.Shared.Research.Systems;
using Robust.Shared.Random;

namespace Content.Shared.Imperial.WeeklyMode.Systems;

public sealed class WeeklyResearchOverlaySystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchTechnologyCardsEvent>(OnCards);
        SubscribeLocalEvent<TechnologyDatabaseComponent, ResearchTechnologiesEvent>(OnTechnologies);
    }

    private void OnCards(EntityUid uid, TechnologyDatabaseComponent database, ref ResearchTechnologyCardsEvent args)
    {
        if (!database.WeeklyModeOnly)
            return;

        args.Handled = true;
        var available = EntityManager.System<SharedResearchSystem>().GetAvailableWeeklyTechnologies(uid, database);
        _random.Shuffle(available);
        args.Cards = new();
        foreach (var discipline in database.SupportedDisciplines)
        {
            var selected = available.FirstOrDefault(technology => technology.Branch == discipline);
            if (!string.IsNullOrEmpty(selected.TechnologyId))
                args.Cards.Add(selected.TechnologyId);
        }
    }

    private void OnTechnologies(EntityUid uid, TechnologyDatabaseComponent database, ref ResearchTechnologiesEvent args)
    {
        if (!database.WeeklyModeOnly)
            return;

        args.Handled = true;
        args.Technologies = new();
    }
}
