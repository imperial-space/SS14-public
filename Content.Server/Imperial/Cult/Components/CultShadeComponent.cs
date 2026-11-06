using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Тень (mob/living/basic/shade).</summary>
[RegisterComponent]
public sealed partial class CultShadeComponent : Component
{
    [ViewVariables] public EntityUid? Master;

    /// <summary>release_time: когда тень вышла из камня душ (null — в камне).</summary>
    [ViewVariables] public TimeSpan? ReleaseTime;
}
