using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>emp_shield: костюм, батарея и модули не поддаются ЭМИ.</summary>
[RegisterComponent]
public sealed partial class ImperialModEmpShieldComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Protected = new();
}
