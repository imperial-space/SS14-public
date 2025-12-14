using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed partial class OpenPlagueMenuMessage : EntityEventArgs
{
    public readonly Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> Data;
    public readonly SummaryPlagueData Info;
    public readonly int AllowedPoints;

    public OpenPlagueMenuMessage(Dictionary<ProtoId<MedievalPlagueSymptomPrototype>, MedievalPlagueSymptomData> data, SummaryPlagueData info, int points)
    {
        Data = data;
        AllowedPoints = points;
        Info = info;
    }
}
