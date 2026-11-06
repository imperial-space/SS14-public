using Content.Shared.DoAfter;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fission;

[Serializable, NetSerializable]
public sealed class FissionCentrifugeUiState(List<string> options) : BoundUserInterfaceState
{
    public readonly List<string> Options = options;
}
