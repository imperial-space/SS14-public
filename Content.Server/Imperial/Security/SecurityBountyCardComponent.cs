using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

/// <summary>registered_account.civilian_bounty: задание хранится на ID-карте.</summary>
[RegisterComponent]
public sealed partial class SecurityBountyCardComponent : Component
{
    [ViewVariables]
    public SecurityBounty? Current;

    [ViewVariables]
    public List<SecurityBounty>? Options;

    /// <summary>bounty_timer: 5 минут между запросами.</summary>
    [ViewVariables]
    public TimeSpan NextRequest;
}
