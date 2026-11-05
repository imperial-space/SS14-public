using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

[Serializable, NetSerializable]
public sealed class PandemicBoundUserInterfaceState : BoundUserInterfaceState
{
    public bool Ready { get; }
    public bool HasBeaker { get; }
    public bool HasBlood { get; }
    public float Volume { get; }
    public float Capacity { get; }
    public string Dna { get; }
    public string BloodType { get; }
    public PandemicDiseaseData[] Diseases { get; }
    public PandemicResistanceData[] Resistances { get; }

    public PandemicBoundUserInterfaceState(bool ready, bool hasBeaker, bool hasBlood, float volume, float capacity,
        string dna, string bloodType, PandemicDiseaseData[] diseases, PandemicResistanceData[] resistances)
    {
        Ready = ready;
        HasBeaker = hasBeaker;
        HasBlood = hasBlood;
        Volume = volume;
        Capacity = capacity;
        Dna = dna;
        BloodType = bloodType;
        Diseases = diseases;
        Resistances = resistances;
    }
}
