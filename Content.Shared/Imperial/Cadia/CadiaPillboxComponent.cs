using Content.Shared.DoAfter;
using Content.Shared.Projectiles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>ДОТ: коробка со стенами-фикстурами, ставит на свой купол орудие. Своим внутри крыша полупрозрачна.</summary>
[RegisterComponent]
public sealed partial class CadiaPillboxComponent : Component
{
    [DataField]
    public EntProtoId GunProto = "CadiaPillboxGun";

    /// <summary>Где на спрайте купол, относительно центра коробки.</summary>
    [DataField]
    public System.Numerics.Vector2 GunOffset = new(0f, 0.69f);

    /// <summary>Половина внутреннего помещения: кто внутри, стреляет наружу и видит сквозь крышу.</summary>
    [DataField]
    public float InteriorHalf = 1.25f;

    [ViewVariables]
    public EntityUid? Gun;
}
