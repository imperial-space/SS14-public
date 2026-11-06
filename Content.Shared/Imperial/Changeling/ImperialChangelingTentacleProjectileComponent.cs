using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Changeling;

/// <summary>/obj/projectile/tentacle.</summary>
[RegisterComponent]
public sealed partial class ImperialChangelingTentacleProjectileComponent : Component
{
    [DataField]
    public TimeSpan TentacleHitDelay = TimeSpan.FromSeconds(0.6);

    [DataField]
    public float TentacleHitBaseThrowSpeed = 6f;
}
