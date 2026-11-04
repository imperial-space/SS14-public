using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

// ═══════════════════════ Секторы ═══════════════════════

/// <summary>Что показать о секторе на панели войны.</summary>
[Serializable, NetSerializable]
public struct CadiaSectorInfo
{
    public string Letter;
    public CadiaFaction? Owner;
    public CadiaFaction? Capturer;
    public float Progress;
    public bool Contested;
    public System.Numerics.Vector2 Position;
    public int Map;
}
