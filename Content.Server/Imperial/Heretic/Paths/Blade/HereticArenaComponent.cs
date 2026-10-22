using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Heretic.Paths.Blade;

[RegisterComponent]
public sealed partial class HereticArenaComponent : Component
{
    [DataField]
    public EntityUid Heretic;

    [DataField]
    public List<EntityUid> Walls = new();

    [DataField]
    public List<EntityUid> Participants = new();

    [DataField]
    public EntityUid FloorGrid;

    [DataField]
    public List<uint> FloorDecals = new();
}
