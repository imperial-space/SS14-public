using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>
/// Ключ внешнего вида для дробилки ксено-слаймов.
/// </summary>
[Serializable, NetSerializable]
public enum XenoSlimeGrinderVisuals : byte
{
    State
}

/// <summary>
/// Состояния дробилки для спрайта.
/// </summary>
[Serializable, NetSerializable]
public enum XenoSlimeGrinderState : byte
{
    Off,
    On
}
