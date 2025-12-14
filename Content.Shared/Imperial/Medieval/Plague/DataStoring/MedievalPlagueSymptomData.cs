using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Plague;

[Serializable, NetSerializable]
public sealed class MedievalPlagueSymptomData
{
    public int Points = 0;
    public bool Unlocked = false;
}
