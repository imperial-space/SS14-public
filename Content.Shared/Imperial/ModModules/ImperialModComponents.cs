using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>
/// Тип модуля МОДа (MODULE_PASSIVE / USABLE / TOGGLE / ACTIVE в TG).
/// </summary>
public enum ImperialModModuleType : byte
{
    /// <summary>Работает сам, пока костюм активен.</summary>
    Passive,

    /// <summary>Разовое применение кнопкой.</summary>
    Usable,

    /// <summary>Включается и выключается кнопкой.</summary>
    Toggle,

    /// <summary>Выбранный модуль: выдаёт устройство в руку (device) или ждёт клика.</summary>
    Active,
}

/// <summary>
/// Части костюма, которые должны быть развёрнуты для работы модуля (required_slots в TG).
/// Перчатки и ботинки МОДов сборки не разворачиваются отдельно, их заменяет нагрудник.
/// </summary>
[Flags]
public enum ImperialModPart : byte
{
    None = 0,
    Back = 1 << 0,
    Outer = 1 << 1,
    Head = 1 << 2,
}

/// <summary>
/// Блок управления МОДа: хранит модули, считает сложность и питает их от своей батареи.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModSuitComponent : Component
{
    public const string ContainerId = "imperial-mod-modules";

    /// <summary>complexity_max: предельная суммарная сложность модулей.</summary>
    [DataField, AutoNetworkedField]
    public int MaxComplexity = 15;

    /// <summary>open: открыта крышка — можно ставить и снимать модули, костюм не работает.</summary>
    [DataField, AutoNetworkedField]
    public bool Open;

    /// <summary>dna_lock: ДНК владельца. Костюм не работает на ком-то другом.</summary>
    [DataField, AutoNetworkedField]
    public string? LockedDna;

    /// <summary>Модули, установленные при создании костюма.</summary>
    [DataField]
    public List<EntProtoId> StartingModules = new();

    [ViewVariables]
    public Container Modules = default!;

    /// <summary>Носитель, которому сейчас выданы действия модулей.</summary>
    [ViewVariables]
    public EntityUid? Wearer;

    /// <summary>Накопитель времени до следующего тика расхода энергии.</summary>
    [ViewVariables]
    public float Accumulator;
}

/// <summary>
/// Модуль МОДа (/obj/item/mod/module).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModModuleComponent : Component
{
    [DataField]
    public ImperialModModuleType ModuleType = ImperialModModuleType.Passive;

    /// <summary>complexity: сколько места модуль занимает в костюме.</summary>
    [DataField]
    public int Complexity;

    /// <summary>idle_power_cost в долях DEFAULT_CHARGE_DRAIN за секунду.</summary>
    [DataField]
    public float IdlePower;

    /// <summary>active_power_cost в долях DEFAULT_CHARGE_DRAIN за секунду.</summary>
    [DataField]
    public float ActivePower;

    /// <summary>use_energy_cost в долях DEFAULT_CHARGE_DRAIN за применение.</summary>
    [DataField]
    public float UsePower;

    /// <summary>cooldown_time.</summary>
    [DataField]
    public TimeSpan Cooldown = TimeSpan.Zero;

    /// <summary>
    /// incompatible_modules: модули с общей группой нельзя поставить вместе.
    /// </summary>
    [DataField]
    public List<string> Groups = new();

    /// <summary>required_slots.</summary>
    [DataField]
    public ImperialModPart RequiredParts = ImperialModPart.Back;

    /// <summary>Работает при неактивном костюме (MODULE_ALLOW_INACTIVE).</summary>
    [DataField]
    public bool AllowInactive;

    /// <summary>Часть костюма, на которую вешаются <see cref="PartComponents"/>.</summary>
    [DataField]
    public ImperialModPart EffectPart = ImperialModPart.Back;

    /// <summary>Компоненты на часть костюма, пока эффект модуля включён (HUD, защита глаз и т. п.).</summary>
    [DataField]
    public ComponentRegistry PartComponents = new();

    /// <summary>Компоненты на носителя, пока эффект модуля включён.</summary>
    [DataField]
    public ComponentRegistry WearerComponents = new();

    /// <summary>device: предмет, который активный модуль выдаёт в руку.</summary>
    [DataField]
    public EntProtoId? Device;

    [ViewVariables]
    public EntityUid? DeviceEntity;

    [DataField]
    public EntProtoId Action = "ImperialActionModModule";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

    /// <summary>active: модуль включён (переключаемый или выбранный).</summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    [ViewVariables]
    public TimeSpan NextUse;

    /// <summary>Эффект сейчас наложен (компоненты выданы).</summary>
    [ViewVariables]
    public bool EffectApplied;

    /// <summary>Какие компоненты модуль реально добавил и куда (чтобы не снять чужие).</summary>
    [ViewVariables]
    public List<(EntityUid Target, Type Type)> AppliedComponents = new();
}

/// <summary>Кнопка модуля на панели действий носителя.</summary>
public sealed partial class ImperialModModuleActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialModPanelDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class ImperialModRemoveDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetEntity Module;

    public ImperialModRemoveDoAfterEvent(NetEntity module)
    {
        Module = module;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>Модуль применён (MODULE_USABLE, on_use). Поднимается на модуле.</summary>
[ByRefEvent]
public record struct ImperialModModuleUsedEvent(EntityUid Suit, EntityUid Wearer, bool Handled = false);

/// <summary>Модуль включён или выключен (on_activation / on_deactivation). Поднимается на модуле.</summary>
[ByRefEvent]
public readonly record struct ImperialModModuleToggledEvent(EntityUid Suit, EntityUid? Wearer, bool Active);

/// <summary>Модуль активен, тик раз в секунду (on_active_process). Поднимается на модуле.</summary>
[ByRefEvent]
public readonly record struct ImperialModModuleProcessEvent(EntityUid Suit, EntityUid Wearer, float FrameTime);

/// <summary>Проверка, можно ли включить модуль (COMSIG_MODULE_TRIGGERED). Поднимается на модуле.</summary>
[ByRefEvent]
public record struct ImperialModModuleActivateAttemptEvent(EntityUid Suit, EntityUid Wearer, bool Cancelled = false);

/// <summary>Устройство, выданное активным модулем в руку (device). Возвращается в модуль при выключении.</summary>
[RegisterComponent]
public sealed partial class ImperialModDeviceComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}

/// <summary>bikehorn: звук при применении.</summary>
[RegisterComponent]
public sealed partial class ImperialModSoundModuleComponent : Component
{
    [DataField(required: true)]
    public Robust.Shared.Audio.SoundSpecifier Sound = default!;
}

/// <summary>gps: применение сообщает координаты носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModGpsModuleComponent : Component;

/// <summary>thermal_regulator: держит температуру тела носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModThermalRegulatorComponent : Component
{
    /// <summary>temperature_setting (BODYTEMP_NORMAL).</summary>
    [DataField]
    public float Target = 310.15f;

    /// <summary>Доля разницы температур, выравниваемая за секунду.</summary>
    [DataField]
    public float Rate = 0.08f;
}

/// <summary>joint_torsion: заряжает батарею костюма при ходьбе.</summary>
[RegisterComponent]
public sealed partial class ImperialModJointTorsionComponent : Component
{
    /// <summary>power_per_step в долях DEFAULT_CHARGE_DRAIN за пройденный тайл.</summary>
    [DataField]
    public float PowerPerStep = 0.45f;

    [ViewVariables]
    public System.Numerics.Vector2? LastPosition;
}

/// <summary>Модуль установлен в костюм или извлечён из него (on_install / on_uninstall). Поднимается на модуле.</summary>
[ByRefEvent]
public readonly record struct ImperialModModuleInstalledEvent(EntityUid Suit, bool Installed);

/// <summary>mister: устройство пополняется реагентом, пока модуль включён.</summary>
[RegisterComponent]
public sealed partial class ImperialModDeviceRefillComponent : Component
{
    [DataField(required: true)]
    public Robust.Shared.Prototypes.ProtoId<Content.Shared.Chemistry.Reagent.ReagentPrototype> Reagent;

    /// <summary>Единиц в секунду.</summary>
    [DataField]
    public float Rate = 2f;

    [DataField]
    public string Solution = "spray";
}

/// <summary>storage: добавляет рядов в хранилище блока управления.</summary>
[RegisterComponent]
public sealed partial class ImperialModStorageModuleComponent : Component
{
    [DataField]
    public int Rows = 1;

    [ViewVariables]
    public List<Robust.Shared.Maths.Box2i>? OriginalGrid;
}

/// <summary>holster: хранит одно оружие и выдаёт его в руку.</summary>
[RegisterComponent]
public sealed partial class ImperialModHolsterComponent : Component
{
    public const string ContainerId = "imperial-mod-holster";

    [DataField]
    public Robust.Shared.Audio.SoundSpecifier Sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Weapons/Guns/Empty/empty.ogg");
}

/// <summary>orebag: собирает руду под ногами, применение высыпает её.</summary>
[RegisterComponent]
public sealed partial class ImperialModOreBagComponent : Component
{
    public const string ContainerId = "imperial-mod-ores";

    [ViewVariables]
    public TimeSpan NextPickup;

    /// <summary>Не подбирать руду, пока носитель стоит на только что высыпанной куче.</summary>
    [ViewVariables]
    public System.Numerics.Vector2? DropPosition;
}

/// <summary>dna_lock: применение привязывает костюм к ДНК носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModDnaLockComponent : Component;

/// <summary>emp_shield: костюм, батарея и модули не поддаются ЭМИ.</summary>
[RegisterComponent]
public sealed partial class ImperialModEmpShieldComponent : Component
{
    [ViewVariables]
    public List<EntityUid> Protected = new();
}

/// <summary>Метка защищённой от ЭМИ сущности (ставится модулем ЭМИ-щита).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModEmpProtectedComponent : Component;

/// <summary>mouthhole: снимает блокировку еды со шлема.</summary>
[RegisterComponent]
public sealed partial class ImperialModMouthholeComponent : Component
{
    [ViewVariables]
    public EntityUid? Helmet;
}

/// <summary>status_readout: динамик костюма громко сообщает о смерти носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModFlatlineComponent : Component
{
    [DataField]
    public Robust.Shared.Audio.SoundSpecifier Sound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Imperial/ModModules/flatline.ogg");
}
