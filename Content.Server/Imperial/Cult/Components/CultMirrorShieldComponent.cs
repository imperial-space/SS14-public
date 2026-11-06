using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Mirror shield.</summary>
[RegisterComponent]
public sealed partial class CultMirrorShieldComponent : Component
{
    [DataField] public int Illusions = 2;
    [ViewVariables] public List<TimeSpan> Restore = new();

    [DataField]
    public TimeSpan MirrorBlockDelay = TimeSpan.FromSeconds(45);

    [DataField]
    public TimeSpan MirrorThrowHitSeconds = TimeSpan.FromSeconds(3f);

    [DataField]
    public TimeSpan MirrorBlockSeconds = TimeSpan.FromSeconds(2.5f);

    [DataField]
    public float MirrorBlockChance = 0.5f;

    [DataField]
    public float MirrorBlockChance2 = 0.6f;
}
