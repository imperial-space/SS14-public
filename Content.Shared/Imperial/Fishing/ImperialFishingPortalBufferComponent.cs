using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Мультитул запомнил генератор порталов (buffer).</summary>
[RegisterComponent]
public sealed partial class ImperialFishingPortalBufferComponent : Component
{
    [ViewVariables]
    public EntityUid Portal;
}
