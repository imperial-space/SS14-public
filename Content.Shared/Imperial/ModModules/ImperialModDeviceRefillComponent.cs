using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>mister: устройство пополняется реагентом, пока модуль включён.</summary>
[RegisterComponent]
public sealed partial class ImperialModDeviceRefillComponent : Component
{
    [DataField(required: true)]
    public Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Reagent;

    /// <summary>Единиц в секунду.</summary>
    [DataField]
    public float Rate = 2f;

    [DataField]
    public string Solution = "spray";
}
