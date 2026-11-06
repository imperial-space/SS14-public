using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Бомбоотсек: сбрасывает бомбу на поверхность под бортом, только из неба.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipBombBayComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Bombs = 4;

    [DataField]
    public int MaxBombs = 4;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.5);

    /// <summary>Сколько бомба летит до земли.</summary>
    [DataField]
    public TimeSpan FallTime = TimeSpan.FromSeconds(3);

    [DataField]
    public float Intensity = 90f;

    [ViewVariables]
    public TimeSpan NextDrop;
}
