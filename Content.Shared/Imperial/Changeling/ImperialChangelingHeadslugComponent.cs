using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/mob/living/basic/headslug.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingHeadslugComponent : Component
{
    /// <summary>egg_lain.</summary>
    [DataField]
    public bool EggLain;

    /// <summary>addtimer(death, 10 SECONDS) после кладки.</summary>
    [ViewVariables]
    public TimeSpan? DieAt;

    [DataField]
    public TimeSpan DieDelay = TimeSpan.FromSeconds(10);
}
