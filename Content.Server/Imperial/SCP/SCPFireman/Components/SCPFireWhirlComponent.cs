using System.Numerics;

namespace Content.Server.Imperial.SCP.SCPFireman.Components;

[RegisterComponent]
public sealed partial class SCPFireWhirlComponent : Component
{
    [DataField("owner")]
    public EntityUid FireOwner;

    [DataField("speed")]
    public float Speed = 4f;

    [DataField("lifetime")]
    public TimeSpan Lifetime = TimeSpan.FromSeconds(30);

    [DataField("igniteRadius")]
    public float IgniteRadius = 0.6f;

    [DataField("effectInterval")]
    public TimeSpan EffectInterval = TimeSpan.FromSeconds(0.25f);

    [ViewVariables]
    public Vector2 Direction = Vector2.UnitX;

    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public TimeSpan NextEffectTime;
}
