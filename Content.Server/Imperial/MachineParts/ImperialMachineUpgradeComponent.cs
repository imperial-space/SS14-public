using System.Linq;
using System.Reflection;
using Content.Server.Construction;
using Content.Server.Construction.Components;
using Content.Shared.Construction.Components;
using Content.Shared.Examine;
using Content.Shared.Imperial.MachineParts;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MachineParts;

/// <summary>Исходные значения улучшаемых полей машины (до учёта деталей).</summary>
[RegisterComponent]
public sealed partial class ImperialMachineUpgradeComponent : Component
{
    [ViewVariables]
    public Dictionary<string, object?> Base = new();
}
