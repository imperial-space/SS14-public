using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

[Serializable, NetSerializable]
public sealed class PandemicResistanceData
{
    public string Id { get; }
    public string Name { get; }

    public PandemicResistanceData(string id, string name)
    {
        Id = id;
        Name = name;
    }
}
