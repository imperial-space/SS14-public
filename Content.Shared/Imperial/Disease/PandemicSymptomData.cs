using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

[Serializable, NetSerializable]
public sealed class PandemicSymptomData
{
    public string Name { get; }
    public string Description { get; }
    public int Stealth { get; }
    public int Resistance { get; }
    public int StageSpeed { get; }
    public int Transmission { get; }
    public int Level { get; }
    public string Cure { get; }
    public string[] Thresholds { get; }

    public PandemicSymptomData(string name, string description, int stealth, int resistance, int stageSpeed,
        int transmission, int level, string cure, string[] thresholds)
    {
        Name = name;
        Description = description;
        Stealth = stealth;
        Resistance = resistance;
        StageSpeed = stageSpeed;
        Transmission = transmission;
        Level = level;
        Cure = cure;
        Thresholds = thresholds;
    }
}
