using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Карта-бункер зенитной позиции.</summary>
[RegisterComponent]
public sealed partial class CadiaEmplacementInteriorComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;
}
