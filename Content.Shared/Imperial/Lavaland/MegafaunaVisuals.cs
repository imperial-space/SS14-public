using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Lavaland;

/// <summary>Внешний вид мегафауны: icon_state, add_atom_colour и alpha из SS13.</summary>
[Serializable, NetSerializable]
public enum MegafaunaVisuals : byte
{
    /// <summary>string — состояние спрайта базового слоя (пусто = исходное).</summary>
    State,
    /// <summary>Color — цвет спрайта.</summary>
    Color,
    /// <summary>string — состояние второго (светящегося) слоя, пусто = скрыть.</summary>
    Overlay,
    /// <summary>Color — цвет второго слоя (geyser_soup по цвету реагента).</summary>
    OverlayColor,
}
