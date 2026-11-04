using Content.Shared.Atmos;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

/// <summary>Болтерный ключ (wrench/bolter): поднимает и опускает болты шлюза без питания.</summary>
[RegisterComponent]
public sealed partial class ImperialBolterWrenchComponent : Component
{
    [DataField]
    public float Delay = 3f;
}
