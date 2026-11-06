using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>computer/prisoner/gulag_teleporter_computer.</summary>
[RegisterComponent]
public sealed partial class GulagConsoleComponent : Component
{
    public const string IdSlot = "gulag_id";

    /// <summary>default_goal.</summary>
    [DataField]
    public int DefaultGoal = 200;

    [ViewVariables]
    public EntityUid? Teleporter;

    [ViewVariables]
    public EntityUid? Beacon;

    [DataField]
    public TimeSpan TeleportDuration = TimeSpan.FromSeconds(4);
}
