using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Под действием Акта веры.</summary>
[RegisterComponent]
public sealed partial class CadiaFaithShieldComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}
