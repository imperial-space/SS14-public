using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Индуктор TG: заряжает батареи и ячейки других устройств от своей ячейки.</summary>
[RegisterComponent]
public sealed partial class ImperialInducerComponent : Component
{
    /// <summary>Сколько энергии передаётся за одну итерацию (power_transfer).</summary>
    [DataField]
    public float Transfer = 200f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1f);
}
