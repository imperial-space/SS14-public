using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>Метка защищённой от ЭМИ сущности (ставится модулем ЭМИ-щита).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModEmpProtectedComponent : Component;
