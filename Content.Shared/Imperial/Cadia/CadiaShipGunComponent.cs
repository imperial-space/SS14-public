using Content.Shared.Actions;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>Пушка на корпусе шаттла: крутится за прицелом стрелка с поста на том же борту.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipGunComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Projectile;

    [DataField]
    public float Speed = 40f;

    /// <summary>Разброс в градусах.</summary>
    [DataField]
    public float Spread = 2.5f;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.2);

    [DataField]
    public float TurnRate = 120f;

    [DataField, AutoNetworkedField]
    public int Ammo = 400;

    [DataField]
    public int MaxAmmo = 400;

    /// <summary>Урон очереди по земле, когда борт в небе.</summary>
    [DataField]
    public float GroundDamage = 14f;

    /// <summary>Сила взрыва на земле (ракеты); 0 — только прямой урон.</summary>
    [DataField]
    public float GroundExplosion;

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Weapons/Guns/Gunshots/lmg.ogg");

    /// <summary>Мировой угол ствола (0 — на север).</summary>
    [DataField, AutoNetworkedField]
    public Angle Angle;

    [ViewVariables]
    public TimeSpan NextShot;
}
