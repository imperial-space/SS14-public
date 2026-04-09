using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Imperial.TerrorSpider.Components;

[RegisterComponent]
public sealed partial class TerrorSpiderGuardianShieldBarrierComponent : Component
{
    [DataField]
    public string PassTag = "TerrorSpider";
}
