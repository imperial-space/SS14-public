using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

[Serializable, NetSerializable]
public sealed class PandemicDiseaseData
{
    public int Index { get; }
    public string Name { get; }
    public string Agent { get; }
    public string Description { get; }
    public string Spread { get; }
    public string CureText { get; }
    public bool Advanced { get; }
    public bool CanRename { get; }
    public int Resistance { get; }
    public int StageSpeed { get; }
    public int Stealth { get; }
    public int Transmission { get; }
    public int Severity { get; }
    public PandemicSymptomData[] Symptoms { get; }

    public PandemicDiseaseData(int index, string name, string agent, string description, string spread, string cureText,
        bool advanced, bool canRename, int resistance, int stageSpeed, int stealth, int transmission, int severity,
        PandemicSymptomData[] symptoms)
    {
        Index = index;
        Name = name;
        Agent = agent;
        Description = description;
        Spread = spread;
        CureText = cureText;
        Advanced = advanced;
        CanRename = canRename;
        Resistance = resistance;
        StageSpeed = stageSpeed;
        Stealth = stealth;
        Transmission = transmission;
        Severity = severity;
        Symptoms = symptoms;
    }
}
