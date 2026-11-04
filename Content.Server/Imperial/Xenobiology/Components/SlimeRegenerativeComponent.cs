using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/regenerative: лечит, как регенеративное ядро легиона.</summary>
[RegisterComponent]
public sealed partial class SlimeRegenerativeComponent : Component
{
    /// <summary>regenerative/bluespace: место создания.</summary>
    [ViewVariables]
    public EntityCoordinates? Origin;
}
