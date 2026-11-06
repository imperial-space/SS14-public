using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Hypertorus;

[Serializable, NetSerializable]
public enum HypertorusVisuals : byte
{
    /// <summary>Детали связаны интерфейсом (active).</summary>
    Active,

    /// <summary>Панель открыта отвёрткой (panel_open).</summary>
    Open,

    /// <summary>Порт треснул от давления модератора (cracked).</summary>
    Cracked,
}
