using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Fugitives;

/// <summary>Полупрозрачность маскировки (alpha 35 из 255).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialCamouflagedComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Alpha = 35f / 255f;

    [DataField]
    public float CamouflageEmpChance = 0.3f;
}
