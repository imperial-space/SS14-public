using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>/obj/machinery/byteforge: материализует награду рядом с сервером.</summary>
[RegisterComponent]
public sealed partial class ByteforgeComponent : Component
{
    [ViewVariables]
    public List<(EntityUid Cache, TimeSpan At)> Pending = new();
}
