using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Kitchen;

/// <summary>/datum/element/processable: инструментом превращается в несколько других предметов.</summary>
[RegisterComponent]
public sealed partial class ImperialProcessableComponent : Component
{
    [DataField]
    public List<ImperialProcessEntry> Entries = new();
}
