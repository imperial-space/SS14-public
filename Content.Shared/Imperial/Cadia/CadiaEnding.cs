using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Концовки ═══════════════════════

/// <summary>Чем кончилась война: по этому выбирается финальная сцена.</summary>
[Serializable, NetSerializable]
public enum CadiaEnding : byte
{
    Held,
    Banished,
    Exterminatus,
    Retreat,
    Bitter,
    Annihilation,
    Triumph,
}
