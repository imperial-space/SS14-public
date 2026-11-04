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

/// <summary>Парашютный ранец: действие «Прыгнуть» с борта, сам раскрывается при падении с неба. Одноразовый.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaParachuteComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Deployed;

    [DataField]
    public TimeSpan Descent = TimeSpan.FromSeconds(6);

    /// <summary>Во сколько раз замедлен спуск по горизонтали.</summary>
    [DataField]
    public float DriftSpeed = 0.6f;
}
