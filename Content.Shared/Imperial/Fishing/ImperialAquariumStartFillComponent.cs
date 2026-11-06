using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fishing;

/// <summary>Стартовое наполнение аквариума: один из вариантов рыбы со своим кормом (аквариум юриста).</summary>
[RegisterComponent]
public sealed partial class ImperialAquariumStartFillComponent : Component
{
    [DataField(required: true)]
    public List<ImperialAquariumStartFillEntry> Variants = new();
}
