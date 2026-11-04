using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Трудовой лагерь ───────────────────────────────────────────────────────

/// <summary>card/id/advanced/prisoner: цель и набранные очки. Вешается на любую ID-карту.</summary>
[RegisterComponent]
public sealed partial class GulagPrisonerIdComponent : Component
{
    [DataField]
    public int Goal;

    [DataField]
    public int Points;
}
