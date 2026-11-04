using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

/// <summary>Метки на карте домена (obj/effect/landmark/bitrunning/*).</summary>
[RegisterComponent]
public sealed partial class BitrunningLandmarkComponent : Component
{
    [DataField(required: true)]
    public BitrunningLandmarkKind Kind;
}
