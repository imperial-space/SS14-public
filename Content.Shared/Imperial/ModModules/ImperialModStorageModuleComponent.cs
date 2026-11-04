using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>storage: добавляет рядов в хранилище блока управления.</summary>
[RegisterComponent]
public sealed partial class ImperialModStorageModuleComponent : Component
{
    [DataField]
    public int Rows = 1;

    [ViewVariables]
    public List<Robust.Shared.Maths.Box2i>? OriginalGrid;
}
