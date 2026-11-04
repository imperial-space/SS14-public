using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Электростул ───────────────────────────────────────────────────────────

/// <summary>assembly/shock_kit.</summary>
[RegisterComponent]
public sealed partial class ShockKitComponent : Component
{
    [DataField]
    public EntProtoId ChairProto = "ImperialElectricChair";
}
