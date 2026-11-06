using Content.Shared.Imperial.MartialArts;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.MartialArts;

/// <summary>status_effect/judo_armbar: «рычаг» дзюдо.</summary>
[RegisterComponent]
public sealed partial class JudoArmbarComponent : Component
{
    [ViewVariables]
    public TimeSpan ExpiresAt;
}
