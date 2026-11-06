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

/// <summary>Пост стрелка: один стрелок управляет всеми пушками и отсеками своего борта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipGunConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    /// <summary>Во сколько раз отдалить камеру стрелка.</summary>
    [DataField]
    public float Zoom = 1.6f;
}
