using Content.Shared.Imperial.Security;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Security;

// ─── Задания СБ ────────────────────────────────────────────────────────────

public enum SecurityBountyKind : byte
{
    Patrol,
    Contraband,
}
