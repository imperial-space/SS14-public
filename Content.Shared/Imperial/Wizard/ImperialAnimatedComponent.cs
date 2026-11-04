using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Оживлённый предмет (animate_atom_living): облик берётся с прототипа предмета.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialAnimatedComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? Proto;
}
