using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/prismatic: бесконечная кисть для пола.</summary>
[RegisterComponent]
public sealed partial class SlimePrismaticComponent : Component
{
    [DataField]
    public Color PaintColor = Color.White;

    /// <summary>prismatic/grey смывает краску.</summary>
    [DataField]
    public bool Remover;
}
