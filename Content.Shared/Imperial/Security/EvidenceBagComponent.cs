using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Пакет для улик (evidencebag) ──────────────────────────────────────────

/// <summary>Пакет для улик. Общий — клиент рисует лежащий внутри предмет.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class EvidenceBagComponent : Component
{
    public const string ContainerId = "storagebase";
}
