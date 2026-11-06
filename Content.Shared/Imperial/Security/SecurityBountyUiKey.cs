using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Задания СБ (civilian bounty, security) ────────────────────────────────

[Serializable, NetSerializable]
public enum SecurityBountyUiKey : byte
{
    Key,
}
