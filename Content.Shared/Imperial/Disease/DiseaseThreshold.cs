using Content.Shared.Body;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Disease;

/// <summary>Порог симптома для окна PanD.E.M.I.C. (threshold_descs).</summary>
[DataDefinition, Serializable, NetSerializable]
public sealed partial class DiseaseThreshold
{
    /// <summary>Какое свойство нужно набрать.</summary>
    [DataField(required: true)]
    public DiseaseProperty Property;

    [DataField(required: true)]
    public int Value;

    [DataField(required: true)]
    public LocId Description;
}
