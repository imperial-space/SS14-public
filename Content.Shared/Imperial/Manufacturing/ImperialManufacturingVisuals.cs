using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Manufacturing;

[Serializable, NetSerializable]
public enum ImperialManufacturingVisuals : byte
{
    /// <summary>Основное состояние спрайта.</summary>
    State,

    /// <summary>Стрелки входа/выхода: «dir:#цвет;…» (generate_io_overlays).</summary>
    Io,

    /// <summary>Стрелки маршрутизатора: «dir:in|out|bl;…».</summary>
    Router,

    /// <summary>Мигающее наложение (crafter_printing, lathe_printing, router_operate:dir); пусто — нет.</summary>
    Flick,
}
