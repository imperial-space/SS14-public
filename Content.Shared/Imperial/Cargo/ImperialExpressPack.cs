using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cargo;

[Serializable, NetSerializable]
public sealed class ImperialExpressPack
{
    public string Id = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public string Category = string.Empty;
    public int Cost;
}
