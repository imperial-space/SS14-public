using Content.Shared.DoAfter;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Security;

// ─── Плакаты розыска (wanted_poster) ───────────────────────────────────────

[Serializable, NetSerializable]
public enum WantedPosterVisuals : byte
{
    Missing,
}
