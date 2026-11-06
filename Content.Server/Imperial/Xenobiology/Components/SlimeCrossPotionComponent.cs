using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>Зелья кроссбридинга (_potions.dm).</summary>
[RegisterComponent]
public sealed partial class SlimeCrossPotionComponent : Component
{
    /// <summary>extractcloner, peace, love, spaceproof, lavaproof, reviver, omnistabilizer.</summary>
    [DataField(required: true)]
    public string Kind = string.Empty;

    [DataField]
    public int Uses = 1;
}
