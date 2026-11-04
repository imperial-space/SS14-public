using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>dispenser/mirage: выдаёт в руку гранату.</summary>
[RegisterComponent]
public sealed partial class ImperialModDispenserComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Item;
}
