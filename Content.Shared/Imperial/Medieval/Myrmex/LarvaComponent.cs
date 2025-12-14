using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Medieval.Myrmex;

[RegisterComponent]
public sealed partial class LarvaComponent : Component
{
    [DataField] public List<EntityPrototype> Eaten = new();
}


