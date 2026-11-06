using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenobiology.Components;

/// <summary>slimecross/recurring: экстракт сам восстанавливает заряды.</summary>
[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class SlimeRecurringComponent : Component
{
    /// <summary>Время восстановления одного заряда.</summary>
    [DataField]
    public TimeSpan MaxCooldown = TimeSpan.FromSeconds(10);

    /// <summary>Когда восстановится следующий заряд.</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextCharge;
}
