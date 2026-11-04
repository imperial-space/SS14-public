using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

[Serializable, NetSerializable]
public sealed class ImperialFishingPortalBoundUserInterfaceState(List<ImperialFishingPortalOption> options, bool unlink) : BoundUserInterfaceState
{
    public readonly List<ImperialFishingPortalOption> Options = options;

    /// <summary>Меню отвязки мест ловли.</summary>
    public readonly bool Unlink = unlink;
}
