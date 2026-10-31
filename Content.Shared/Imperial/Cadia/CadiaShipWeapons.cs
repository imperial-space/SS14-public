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

/// <summary>Бомбоотсек: сбрасывает бомбу на поверхность под бортом, только из неба.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipBombBayComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Bombs = 4;

    [DataField]
    public int MaxBombs = 4;

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(0.5);

    /// <summary>Сколько бомба летит до земли.</summary>
    [DataField]
    public TimeSpan FallTime = TimeSpan.FromSeconds(3);

    [DataField]
    public float Intensity = 90f;

    [ViewVariables]
    public TimeSpan NextDrop;
}

/// <summary>Авиабомба: заряжается в бомбоотсек.</summary>
[RegisterComponent]
public sealed partial class CadiaShipBombComponent : Component;

/// <summary>Пост стрелка: один стрелок управляет всеми пушками и отсеками своего борта.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipGunConsoleComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Gunner;

    /// <summary>Во сколько раз отдалить камеру стрелка.</summary>
    [DataField]
    public float Zoom = 1.6f;
}

/// <summary>Боец за постом стрелка: камера на борту, курсор — прицел.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaShipGunnerComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid Console;

    [DataField, AutoNetworkedField]
    public EntityUid Grid;

    /// <summary>Какая пушка стреляет; пусто — все сразу.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Selected;

    [ViewVariables]
    public bool HadFov;

    [ViewVariables]
    public bool Firing;

    [ViewVariables]
    public MapCoordinates Aim = MapCoordinates.Nullspace;

    [ViewVariables]
    public List<EntityUid> Actions = new();
}

[Serializable, NetSerializable]
public sealed class CadiaShipAimEvent(NetCoordinates target) : EntityEventArgs
{
    public readonly NetCoordinates Target = target;
}

[Serializable, NetSerializable]
public sealed class CadiaShipFireEvent(bool down) : EntityEventArgs
{
    public readonly bool Down = down;
}

public sealed partial class CadiaShipBombActionEvent : InstantActionEvent;

public sealed partial class CadiaShipSelectGunActionEvent : InstantActionEvent;

public sealed partial class CadiaShipLeaveGunActionEvent : InstantActionEvent;

/// <summary>Парашютный ранец: действие «Прыгнуть» с борта, сам раскрывается при падении с неба. Одноразовый.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaParachuteComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Deployed;

    [DataField]
    public TimeSpan Descent = TimeSpan.FromSeconds(6);

    /// <summary>Во сколько раз замедлен спуск по горизонтали.</summary>
    [DataField]
    public float DriftSpeed = 0.6f;
}

public sealed partial class CadiaParachuteJumpActionEvent : InstantActionEvent;

/// <summary>Идёт спуск: можно рулить стропами, но не стрелять и не драться. По бойцу при этом можно стрелять.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaParachutingComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DriftSpeed = 0.6f;

    [ViewVariables]
    public TimeSpan LandAt;

    [ViewVariables]
    public EntityUid? Canopy;

    /// <summary>Когда кончится разовая анимация раскрытия и появится раскрытый купол.</summary>
    [ViewVariables]
    public TimeSpan? OpenAt;
}

public sealed class CadiaParachutingSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CadiaParachutingComponent, ShotAttemptedEvent>(OnShot);
        SubscribeLocalEvent<CadiaParachutingComponent, AttackAttemptEvent>(OnAttack);
        SubscribeLocalEvent<CadiaParachutingComponent, RefreshMovementSpeedModifiersEvent>(OnSpeed);
    }

    private void OnShot(Entity<CadiaParachutingComponent> ent, ref ShotAttemptedEvent args)
    {
        args.Cancel();
    }

    private void OnAttack(EntityUid uid, CadiaParachutingComponent comp, AttackAttemptEvent args)
    {
        args.Cancel();
    }

    private void OnSpeed(Entity<CadiaParachutingComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        args.ModifySpeed(ent.Comp.DriftSpeed, ent.Comp.DriftSpeed);
    }
}
