using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Огневая консоль в бункере зенитной позиции.</summary>
[RegisterComponent]
public sealed partial class CadiaAntiAirConsoleComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;
}
