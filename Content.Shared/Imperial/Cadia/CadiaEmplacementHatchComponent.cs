using System.Numerics;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Люк бункера: выводит наружу к своей позиции.</summary>
[RegisterComponent]
public sealed partial class CadiaEmplacementHatchComponent : Component
{
    [ViewVariables]
    public EntityUid? Gun;
}
