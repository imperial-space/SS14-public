using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/recurring: экстракт сам восстанавливает заряды.</summary>
[RegisterComponent]
public sealed partial class SlimeRecurringComponent : Component
{
    [DataField]
    public float MaxCooldown = 10f;

    [ViewVariables]
    public float Cooldown;
}
