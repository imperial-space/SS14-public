using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Снаряд «Эхо перчатки» джаггернаута.</summary>
[RegisterComponent]
public sealed partial class CultGauntletProjectileComponent : Component
{
    [DataField]
    public TimeSpan GauntletHitTime = TimeSpan.FromSeconds(5);

    [DataField]
    public float GauntletHitRange = 1.5f;

    [DataField]
    public float GauntletHitAmount = 90f;
}
