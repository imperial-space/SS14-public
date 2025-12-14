using System.Numerics;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Medieval.Boss;

[RegisterComponent]
public sealed partial class BossBullethellSourceComponent : Component
{
    [DataField(required: true)]
    public EntProtoId WeaponProto;

    [DataField]
    public int Shots = 10;

    [DataField]
    public int DegreesPerShot = 15;

    [DataField]
    public bool Negative = false;

    [DataField]
    public bool RandomizeNegative = true;

    [DataField]
    public bool RandomRotation = true;

    [DataField]
    public float Delay = 0.5f;

    [ViewVariables(VVAccess.ReadWrite)]
    public int CurShot = 1;

    [ViewVariables(VVAccess.ReadWrite)]
    public EntityUid Weapon;

    [ViewVariables(VVAccess.ReadWrite)]
    public TimeSpan NextShot;

    [ViewVariables(VVAccess.ReadWrite)]
    public Vector2 TargetPos;
}
