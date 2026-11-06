using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>burning/charged/chilling: эффект при сжатии в руке с 10u плазмы.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossActivatedComponent : Component
{
    [DataField]
    public string Solution = "crossbreed";

    [DataField]
    public float PlasmaRequired = 10f;

    /// <summary>Эффект уже запущен (таймер взрыва, призыв и т.п.).</summary>
    [ViewVariables]
    public bool Active;
}
