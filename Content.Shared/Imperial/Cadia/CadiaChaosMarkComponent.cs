using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Метка бога Хаоса на десантнике.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaChaosMarkComponent : Component
{
    [DataField, AutoNetworkedField]
    public CadiaChaosGod God;

    /// <summary>Щит варпа Тзинча: следующее попадание поглощается.</summary>
    [ViewVariables]
    public TimeSpan NextWard;

    [ViewVariables]
    public List<EntityUid> ChoiceActions = new();
}
