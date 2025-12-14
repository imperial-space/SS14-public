using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed class SummaryPlagueData
{
    public int Infected = 0;
    public int Immune = 0;
    public int Tier = 0;
    public int Points = 0;
    public int Symptoms = 0;
    public int PlagueGhosts = 0;

    public SummaryPlagueData()
    {

    }

    public SummaryPlagueData(int infected, int immune, int tier, int points, int symptoms, int ghosts)
    {
        Infected = infected;
        Immune = immune;
        Tier = tier;
        Points = points;
        Symptoms = symptoms;
        PlagueGhosts = ghosts;
    }
}
