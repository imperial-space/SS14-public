using Robust.Shared.Timing;

namespace Content.Server.Imperial.MartialArts;

/// <summary>
/// Данные <see cref="MartialArtsSystem"/> для этой сущности.
/// </summary>
[RegisterComponent]
public sealed partial class MartialArtsPendingDisarmComponent : Component
{
    [ViewVariables]
    public (EntityUid Target, GameTick Tick) Value;
}
