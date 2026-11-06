using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Emp;
using Content.Shared.Gravity;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// jetpack (ion): носитель может двигаться в невесомости без газа.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModJetpackUserComponent : Component
{
    [DataField, AutoNetworkedField]
    public float Acceleration = 1f;

    [DataField, AutoNetworkedField]
    public float Friction = 0.25f;

    [DataField, AutoNetworkedField]
    public float WeightlessModifier = 1.2f;
}
