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

/// <summary>Идёт спуск: можно рулить стропами, но не стрелять и не драться. По бойцу при этом можно стрелять.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaParachutingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DriftSpeed = 0.6f;

    [ViewVariables]
    public TimeSpan LandAt;

    [ViewVariables]
    public EntityUid? Canopy;

    /// <summary>Когда кончится разовая анимация раскрытия и появится раскрытый купол.</summary>
    [ViewVariables]
    public TimeSpan? OpenAt;
}
