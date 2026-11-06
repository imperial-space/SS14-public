using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Время жизни иллюзии (addtimer death).</summary>
[RegisterComponent]
public sealed partial class CultIllusionLifeComponent : Component
{
    [ViewVariables] public TimeSpan End;
}
