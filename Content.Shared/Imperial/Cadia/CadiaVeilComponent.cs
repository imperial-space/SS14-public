using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Телекинетический купол и завеса: снимается по таймеру.</summary>
[RegisterComponent]
public sealed partial class CadiaVeilComponent : Component
{
    [ViewVariables]
    public TimeSpan Until;
}
