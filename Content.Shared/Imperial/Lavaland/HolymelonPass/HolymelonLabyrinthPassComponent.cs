using Robust.Shared.GameObjects;
using Robust.Shared.GameStates;
using Robust.Shared.Timing;

namespace Content.Shared.Imperial.Lavaland;

/// <summary>
/// Временный эффект от употребления святого арбуза — позволяет проходить сквозь барьеры Книги лабиринта.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HolymelonLabyrinthPassComponent : Component
{
    [DataField]
    public TimeSpan ExpiresAt;
}
