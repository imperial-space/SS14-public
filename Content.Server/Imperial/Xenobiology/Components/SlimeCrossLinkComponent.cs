using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>chilling/bluespace, chilling/sepia: список связанных существ.</summary>
[RegisterComponent]
public sealed partial class SlimeCrossLinkComponent : Component
{
    [ViewVariables]
    public HashSet<EntityUid> Linked = new();
}
