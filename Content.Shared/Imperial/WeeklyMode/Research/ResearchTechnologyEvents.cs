using Content.Shared.Research.Prototypes;

namespace Content.Shared.Research.Systems;

[ByRefEvent]
public struct ResearchTechnologyCardsEvent
{
    public bool Handled;
    public List<string> Cards;
}

[ByRefEvent]
public struct ResearchTechnologiesEvent
{
    public bool Handled;
    public List<TechnologyPrototype> Technologies;
}
