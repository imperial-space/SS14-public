using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Щит со вспышкой (shield/riot/flash) ───────────────────────────────────

[Serializable, NetSerializable]
public enum StrobeShieldVisuals : byte
{
    Flashing,
}
