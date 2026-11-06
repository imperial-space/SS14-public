using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.TerrorSpider;

/// <summary>obj/item/organ/internal/body_egg/terror_eggs: яйца белого паука в носителе.</summary>
[RegisterComponent]
public sealed partial class TerrorEggsInfectionComponent : Component
{
    [ViewVariables]
    public int CycleNum;

    [ViewVariables]
    public float EggProgress;

    [DataField]
    public float EggProgressPerHatch = 90;

    [ViewVariables]
    public int EggsHatched;

    [DataField]
    public TimeSpan EggsLifeBoozePowerDelay = TimeSpan.FromSeconds(4.5);

    [DataField]
    public TimeSpan HatchEggDuration = TimeSpan.FromSeconds(20);

    [DataField]
    public float EggsRemovalAmount = 15f;
}
