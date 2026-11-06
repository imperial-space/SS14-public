namespace Content.Server.Imperial.Changeling;

/// <summary>Данные генокрада на разуме: в TG они живут в датуме антагониста и переживают тело.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingMindComponent : Component
{
    /// <summary>absorbed_count.</summary>
    [ViewVariables]
    public int AbsorbedCount;

    /// <summary>genetic_points + dna_cost купленного (absorb_changeling).</summary>
    [ViewVariables]
    public int EffectivePoints;

    /// <summary>initial(genetic_points).</summary>
    [ViewVariables]
    public int InitialPoints = 10;
}
