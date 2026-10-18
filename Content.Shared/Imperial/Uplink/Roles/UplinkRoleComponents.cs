using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Roles;

/// <summary>Клоунский боёк (firing_pin/clown/ultra): стрелять могут только клоуны и неуклюжие.</summary>
[RegisterComponent]
public sealed partial class UplinkClownPinComponent : Component
{
    /// <summary>selfdestruct: оружие взрывается у не-клоуна.</summary>
    [DataField]
    public bool SelfDestruct;
}

/// <summary>Оружие с клоунским бойком.</summary>
[RegisterComponent]
public sealed partial class UplinkClownPinnedComponent : Component
{
    [DataField]
    public bool SelfDestruct;
}

/// <summary>Обратный револьвер: у не-клоуна стреляет в стрелка.</summary>
[RegisterComponent]
public sealed partial class UplinkReverseGunComponent : Component
{
    [DataField]
    public float Damage = 60f;
}

/// <summary>Обратный медвежий капкан.</summary>
[RegisterComponent]
public sealed partial class UplinkReverseBearTrapComponent : Component
{
    [DataField]
    public TimeSpan Countdown = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan? KillAt;

    [ViewVariables]
    public int EscapeChance;

    [ViewVariables]
    public TimeSpan NextBeep;
}

[Serializable, NetSerializable]
public sealed partial class UplinkBearTrapForceDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class UplinkBearTrapStruggleDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Мясной крюк: снаряд притягивает цель к стрелку.</summary>
[RegisterComponent]
public sealed partial class UplinkHookProjectileComponent : Component;

/// <summary>Автоинъектор, превращающий в моба (сыворотка магиллитиса).</summary>
[RegisterComponent]
public sealed partial class UplinkPolymorphInjectorComponent : Component
{
    [DataField(required: true)]
    public string Polymorph = string.Empty;
}

/// <summary>Программа «промывки мозгов»: внушает спящей жертве задачу.</summary>
[RegisterComponent]
public sealed partial class UplinkBrainwashDeviceComponent : Component;

[Serializable, NetSerializable]
public sealed partial class UplinkBrainwashDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Продвинутая пластическая хирургия: копирует облик и голос с одного человека на другого.</summary>
[RegisterComponent]
public sealed partial class UplinkPlasticSurgeryComponent : Component
{
    [ViewVariables]
    public EntityUid? Template;

    [ViewVariables]
    public string? TemplateName;
}

[Serializable, NetSerializable]
public sealed partial class UplinkPlasticSurgeryDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Лазерная рука: имплант с лазерным пистолетом в руке.</summary>
[RegisterComponent]
public sealed partial class UplinkArmGunImplantComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Gun;

    [ViewVariables]
    public EntityUid? Spawned;
}

public sealed partial class UplinkToggleArmGunEvent : InstantActionEvent;

/// <summary>Книга мима: даёт заклинание, если читатель — мим.</summary>
[RegisterComponent]
public sealed partial class UplinkMimeryBookComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Action;
}

public sealed partial class UplinkMimeWallEvent : InstantActionEvent;

public sealed partial class UplinkFingerGunEvent : InstantActionEvent;

/// <summary>Его Милость.</summary>
[RegisterComponent]
public sealed partial class UplinkHisGraceComponent : Component
{
    [ViewVariables]
    public bool Awakened;

    [ViewVariables]
    public float Bloodthirst;

    [ViewVariables]
    public float PrevBloodthirst;

    [ViewVariables]
    public float ForceBonus;

    [ViewVariables]
    public int Victims;

    [ViewVariables]
    public bool Ascended;

    [ViewVariables]
    public TimeSpan NextTick;

    [DataField]
    public float BaseForce = 12f;
}

/// <summary>ИИМ Синдиката: борг с этим мозгом получает законы Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieMmiComponent : Component
{
    [DataField]
    public string Laws = "SyndicateStatic";
}

/// <summary>Австраликус: впрыснутый в золотой экстракт, призывает пауков.</summary>
[RegisterComponent]
public sealed partial class UplinkSpiderInjectorComponent : Component
{
    [DataField]
    public EntProtoId Spider = "ImperialAustralicusSpider";

    [DataField]
    public int Amount = 3;
}

/// <summary>Злой росток: семена, прорастающие в злое растение-союзника.</summary>
[RegisterComponent]
public sealed partial class UplinkEvilSeedComponent : Component
{
    [DataField]
    public EntProtoId Mob = "ImperialEvilSeedling";

    [DataField]
    public TimeSpan GrowTime = TimeSpan.FromSeconds(30);

    [ViewVariables]
    public TimeSpan? GrowAt;
}

/// <summary>Пчелиный дымарь: делает пчёл вокруг союзниками.</summary>
[RegisterComponent]
public sealed partial class UplinkBeeSmokerComponent : Component
{
    [DataField]
    public float Range = 5f;
}

/// <summary>Проклятый шар-восьмёрка.</summary>
[RegisterComponent]
public sealed partial class UplinkHauntedEightballComponent : Component
{
    [ViewVariables]
    public TimeSpan? AnswerAt;

    [ViewVariables]
    public EntityUid? Asker;
}

/// <summary>Кейс со встроенным спусковым механизмом: стреляет оружием внутри.</summary>
[RegisterComponent]
public sealed partial class UplinkBriefcaseGunComponent : Component;

/// <summary>Шприцемёт, стреляющий ДНК-инжекторами.</summary>
[RegisterComponent]
public sealed partial class UplinkDnaSyringeGunComponent : Component;

/// <summary>ДНК-инжектор, выпущенный из модифицированного шприцемёта.</summary>
[RegisterComponent]
public sealed partial class UplinkDnaDartComponent : Component
{
    [ViewVariables]
    public EntityUid? Shooter;
}

/// <summary>Машина клоуна: таранит людей и запихивает их внутрь.</summary>
[RegisterComponent]
public sealed partial class UplinkClownCarComponent : Component
{
    [DataField]
    public string Container = "clowncar_victims";

    [DataField]
    public int Capacity = 10;
}

/// <summary>Диск гиперконтузионного диода для эмиттера.</summary>
[RegisterComponent]
public sealed partial class UplinkEmitterDiskComponent : Component
{
    [DataField]
    public EntProtoId Bolt = "ImperialEmitterBoltBlast";

    /// <summary>Во сколько раз реже стреляет эмиттер (fire_rate_mod у TG; гиперконтузионный — 2).</summary>
    [DataField]
    public float FireIntervalMultiplier = 2f;
}

/// <summary>Покупка «Вирус организационной путаницы» — случайные вакансии на станции.</summary>
[RegisterComponent]
public sealed partial class UplinkBureaucraticErrorComponent : Component;
