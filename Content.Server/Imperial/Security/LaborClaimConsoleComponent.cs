using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>labor_claim_console.</summary>
[RegisterComponent]
public sealed partial class LaborClaimConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Stacker;

    [ViewVariables]
    public bool Emagged;

    /// <summary>say_cooldown.</summary>
    [ViewVariables]
    public TimeSpan NextSay;
}
