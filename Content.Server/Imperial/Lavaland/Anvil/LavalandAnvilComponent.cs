using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Server.Imperial.Lavaland.Anvil;

[RegisterComponent, AutoGenerateComponentPause]
public sealed partial class LavalandAnvilComponent : Component
{
    [DataField]
    public string ContainerId = "lavaland_anvil";

    [DataField]
    public int MaxCharges = 2;

    [DataField]
    public int CurrentCharges = 2;

    /// <summary>Время восстановления одного заряда.</summary>
    [DataField]
    public TimeSpan RechargeTime = TimeSpan.FromMinutes(5);

    /// <summary>Когда восстановится следующий заряд (если зарядов меньше максимума).</summary>
    [ViewVariables, AutoPausedField]
    public TimeSpan NextCharge;
}
