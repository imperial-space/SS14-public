using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>Граната-мираж: при срабатывании создаёт голограммы бросившего.</summary>
[RegisterComponent]
public sealed partial class ImperialMirageGrenadeComponent : Component
{
    [DataField]
    public EntProtoId Hologram = "ImperialMirageHologram";

    [DataField]
    public int Count = 4;

    [ViewVariables]
    public EntityUid? OwnerEntity;
}
