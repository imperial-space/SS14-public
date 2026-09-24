using System.Numerics;
using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Map;

namespace Content.Server.Imperial.DeimonFly.DeathNote.Components;

[RegisterComponent]
public sealed partial class DeathNoteFallingVendingComponent : Component
{
    [ViewVariables]
    public EntityUid Target;

    [ViewVariables]
    public EntityUid Parent;

    [ViewVariables]
    public Vector2 StartPosition;

    [ViewVariables]
    public Vector2 ImpactPosition;

    [ViewVariables]
    public Angle StartRotation;

    [ViewVariables]
    public TimeSpan StartedAt;

    [ViewVariables]
    public TimeSpan ImpactAt;

    [ViewVariables]
    public DamageSpecifier Damage = new();

    [ViewVariables]
    public SoundSpecifier? ImpactSound;
}
