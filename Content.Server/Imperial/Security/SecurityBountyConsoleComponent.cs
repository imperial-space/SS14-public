using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>computer/piratepad_control/civilian.</summary>
[RegisterComponent]
public sealed partial class SecurityBountyConsoleComponent : Component
{
    public const string IdSlot = "bounty_id";

    [ViewVariables]
    public EntityUid? Pad;

    [ViewVariables]
    public string StatusReport = string.Empty;

    /// <summary>warmup_time: 3 секунды до отправки.</summary>
    [ViewVariables]
    public TimeSpan? SendAt;
}
