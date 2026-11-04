using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public enum FissionReactorState : byte
{
    Off,
    Starting,
    On,
    Hot,
    Overheat,
    Maintenance,
    Broken,
    Meltdown,
}
