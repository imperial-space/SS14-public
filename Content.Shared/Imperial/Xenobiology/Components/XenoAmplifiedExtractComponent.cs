using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Xenobiology.Components;

/// <summary>
/// Маркер на экстракте ксено-слайма, указывающий что он усилен с помощью
/// XenoCeruleanPotion (Усилитель экстракторов).
/// При наличии этого компонента эффект ProduceReagent производит в 3 раза больше реагента.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class XenoAmplifiedExtractComponent : Component
{
    /// <summary>Множитель количества производимого реагента.</summary>
    [DataField]
    public int Multiplier = 3;
}
