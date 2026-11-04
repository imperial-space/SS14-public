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

/// <summary>Боец за постом стрелка: камера на борту, курсор — прицел.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipGunnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Console;

    [DataField, AutoNetworkedField]
    public EntityUid Grid;

    /// <summary>Какая пушка стреляет; пусто — все сразу.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Selected;

    [ViewVariables]
    public bool HadFov;

    [ViewVariables]
    public bool Firing;

    [ViewVariables]
    public MapCoordinates Aim = MapCoordinates.Nullspace;

    [ViewVariables]
    public List<EntityUid> Actions = new();
}
