using Content.Shared.Imperial.Bitrunning;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Bitrunning;

public enum BitrunningLandmarkKind : byte
{
    Hololadder,
    PermanentExit,
    Goal,
    Cache,
    Curiosity,
    LootSignal,
    MobSegment,
    CrateReplacer,
}
