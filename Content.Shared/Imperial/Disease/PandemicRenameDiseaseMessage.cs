using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

[Serializable, NetSerializable]
public sealed class PandemicRenameDiseaseMessage : BoundUserInterfaceMessage
{
    public int Index { get; }
    public string Name { get; }

    public PandemicRenameDiseaseMessage(int index, string name)
    {
        Index = index;
        Name = name;
    }
}
