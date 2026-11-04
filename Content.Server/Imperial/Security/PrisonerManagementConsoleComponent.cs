using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Консоль управления заключёнными и химимплант ──────────────────────────

/// <summary>computer/prisoner/management.</summary>
[RegisterComponent]
public sealed partial class PrisonerManagementConsoleComponent : Component
{
    public const string IdSlot = "prisoner_id";
}
