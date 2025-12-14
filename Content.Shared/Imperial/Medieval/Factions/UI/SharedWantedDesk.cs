using Content.Shared.Humanoid;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Medieval.Factions;

[Serializable, NetSerializable]
public sealed class WantedDeskBoundUserInterfaceState : BoundUserInterfaceState
{
    public Dictionary<int, WantedData> Wanted;

    public WantedDeskBoundUserInterfaceState(Dictionary<int, WantedData> wanted)
    {
        Wanted = wanted;
    }
}

[Serializable, NetSerializable]
public enum WantedDeskUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum WantedDeskVisuals : byte
{
    Appearance,
    Layer
}

[Serializable, NetSerializable]
public enum WantedDeskVisualState : byte
{
    None,
    Min,
    Medium,
    Full
}
