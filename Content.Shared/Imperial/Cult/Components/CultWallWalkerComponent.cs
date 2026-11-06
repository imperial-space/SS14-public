using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Cult.Components;

/// <summary>wall_walker: жнец проходит сквозь стены культа.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CultWallWalkerComponent : Component
{
    [DataField]
    public List<string> Walls = new() { "WallCult", "WallCultArtificer" };
}
