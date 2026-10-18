using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

// Модули МОДа этапа М3: применение по цели, ядра аномалий, захват, сонар, невидимость и т. п.

/// <summary>Кнопка модуля с выбором цели (on_select_use по клику в TG).</summary>
public sealed partial class ImperialModModuleTargetActionEvent : WorldTargetActionEvent;

/// <summary>Модуль применён по цели. Поднимается на модуле.</summary>
[ByRefEvent]
public record struct ImperialModModuleTargetUsedEvent(EntityUid Suit, EntityUid Wearer, EntityCoordinates Target, EntityUid? TargetEntity, bool Handled = false);

/// <summary>
/// anomaly_locked: модуль работает только с ядром аномалии нужного типа внутри.
/// </summary>
[RegisterComponent]
public sealed partial class ImperialModAnomalyLockComponent : Component
{
    public const string ContainerId = "imperial-mod-core";

    [DataField(required: true)]
    public List<EntProtoId> Cores = new();

    /// <summary>core_removable.</summary>
    [DataField]
    public bool Removable = true;
}

/// <summary>antigrav: носитель парит (forced_gravity 0).</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ImperialModAntigravComponent : Component;

/// <summary>teleporter: телепорт на видимую клетку.</summary>
[RegisterComponent]
public sealed partial class ImperialModTeleporterComponent : Component
{
    [DataField]
    public float Range = 9f;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(1);

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Effects/teleport_arrival.ogg");
}

[Serializable, NetSerializable]
public sealed partial class ImperialModTeleportDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Target;

    public ImperialModTeleportDoAfterEvent(NetCoordinates target)
    {
        Target = target;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>kinesis: притягивает предмет к носителю.</summary>
[RegisterComponent]
public sealed partial class ImperialModKinesisComponent : Component
{
    /// <summary>grab_range.</summary>
    [DataField]
    public float Range = 8f;

    [DataField]
    public float ThrowSpeed = 10f;
}

/// <summary>clamp: грузит ящики в модуль и выгружает их.</summary>
[RegisterComponent]
public sealed partial class ImperialModClampComponent : Component
{
    public const string ContainerId = "imperial-mod-crates";

    /// <summary>grabDelay грейфера меха.</summary>
    [DataField]
    public TimeSpan LoadTime = TimeSpan.FromSeconds(2.5);

    /// <summary>maxContents грейфера меха.</summary>
    [DataField]
    public int MaxCrates = 10;

    /// <summary>Энергия МОДа на захват (grabEnergyDelta).</summary>
    [DataField]
    public float GrabPower = 3f;

    [DataField]
    public Robust.Shared.Audio.SoundSpecifier GrabSound = new Robust.Shared.Audio.SoundPathSpecifier("/Audio/Mecha/sound_mecha_hydraulic.ogg");

    [ViewVariables]
    public EntityUid? AudioStream;
}

[Serializable, NetSerializable]
public sealed partial class ImperialModClampDoAfterEvent : SimpleDoAfterEvent;

/// <summary>criminalcapture / patienttransport: мешок из твёрдого света вокруг цели.</summary>
[RegisterComponent]
public sealed partial class ImperialModCaptureComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Bag;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromSeconds(2.5);

    [DataField]
    public TimeSpan PackupTime = TimeSpan.FromSeconds(1);

    /// <summary>Расход, пока мешок существует (DEFAULT_CHARGE_DRAIN × 3).</summary>
    [DataField]
    public float LinkedPower = 3f;

    [DataField]
    public float MaxRange = 9f;

    [ViewVariables]
    public EntityUid? LinkedBag;

    [ViewVariables]
    public TimeSpan NextDrain;
}

[Serializable, NetSerializable]
public sealed partial class ImperialModCaptureDoAfterEvent : DoAfterEvent
{
    [DataField]
    public NetCoordinates Target;

    [DataField]
    public bool Packup;

    public ImperialModCaptureDoAfterEvent(NetCoordinates target, bool packup)
    {
        Target = target;
        Packup = packup;
    }

    public override DoAfterEvent Clone() => this;
}

/// <summary>Мешок из твёрдого света, созданный модулем захвата.</summary>
[RegisterComponent]
public sealed partial class ImperialModHardlightBagComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}

/// <summary>microwave_beam: разогревает предмет на расстоянии.</summary>
[RegisterComponent]
public sealed partial class ImperialModMicrowaveBeamComponent : Component
{
    [DataField]
    public float Range = 9f;

    /// <summary>Энергия, которую получают растворы предмета, Дж.</summary>
    [DataField]
    public float Heat = 20000f;
}

/// <summary>dispenser/mirage: выдаёт в руку гранату.</summary>
[RegisterComponent]
public sealed partial class ImperialModDispenserComponent : Component
{
    [DataField(required: true)]
    public EntProtoId Item;
}

/// <summary>Граната-мираж: при срабатывании создаёт голограммы бросившего.</summary>
[RegisterComponent]
public sealed partial class ImperialMirageGrenadeComponent : Component
{
    [DataField]
    public EntProtoId Hologram = "ImperialMirageHologram";

    [DataField]
    public int Count = 4;

    [ViewVariables]
    public EntityUid? Owner;
}

/// <summary>Голограмма-мираж: копирует спрайт источника на клиенте.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialMirageHologramComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntityUid? Source;

    [ViewVariables]
    public TimeSpan NextMove;
}

/// <summary>recycler: собирает мусор под ногами и перерабатывает в листы.</summary>
[RegisterComponent]
public sealed partial class ImperialModRecyclerComponent : Component
{
    [DataField]
    public List<ProtoId<Content.Shared.Tag.TagPrototype>> Tags = new() { "Trash" };

    /// <summary>Накопленные материалы (единицы материала).</summary>
    [ViewVariables]
    public Dictionary<string, int> Materials = new();

    [ViewVariables]
    public TimeSpan NextPickup;
}

/// <summary>magnetic_harness: выпавшее оружие примагничивается в слот хранилища костюма.</summary>
[RegisterComponent]
public sealed partial class ImperialModMagHarnessComponent : Component
{
    [ViewVariables]
    public List<(EntityUid Gun, TimeSpan At)> Pending = new();
}

/// <summary>projectile_dampener: поле, гасящее чужие снаряды вокруг носителя.</summary>
[RegisterComponent]
public sealed partial class ImperialModDampenerComponent : Component
{
    [DataField]
    public float Radius = 2f;

    [DataField]
    public float DamageMultiplier = 0.75f;

    [DataField]
    public float SpeedMultiplier = 0.4f;
}

/// <summary>Снаряд уже ослаблен гасителем.</summary>
[RegisterComponent]
public sealed partial class ImperialModDampenedComponent : Component;

public enum ImperialModShootingMode : byte
{
    Off,
    Stormtrooper,
    Sharpshooter,
}

/// <summary>shooting_assistant: режим стрельбы носителя.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialModShootingAssistantComponent : Component
{
    [DataField, AutoNetworkedField]
    public ImperialModShootingMode Mode;
}

/// <summary>Оружие в руке носителя помощника стрелка.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ImperialModAssistedGunComponent : Component
{
    [DataField, AutoNetworkedField]
    public ImperialModShootingMode Mode;
}

/// <summary>stealth: снимается при получении урона.</summary>
[RegisterComponent]
public sealed partial class ImperialModCloakComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}

/// <summary>active_sonar: показывает живых существ вокруг.</summary>
[RegisterComponent]
public sealed partial class ImperialModSonarComponent : Component
{
    [DataField]
    public float Range = 9f;

    [DataField]
    public int Slices = 8;

    [DataField]
    public TimeSpan ScanInterval = TimeSpan.FromSeconds(0.5);

    [DataField]
    public SoundSpecifier Sound = new SoundPathSpecifier("/Audio/Imperial/ModModules/sonar_ping.ogg");

    [ViewVariables]
    public int Slice;

    [ViewVariables]
    public TimeSpan NextScan;
}

/// <summary>pathfinder: имплант, который вызывает костюм к владельцу.</summary>
[RegisterComponent]
public sealed partial class ImperialModPathfinderComponent : Component
{
    [DataField]
    public EntProtoId Implanter = "ImperialModPathfinderImplanter";

    [DataField]
    public TimeSpan TravelTime = TimeSpan.FromSeconds(5);
}

/// <summary>Имплант путеискателя: помнит костюм.</summary>
[RegisterComponent]
public sealed partial class ImperialModPathfinderImplantComponent : Component
{
    [ViewVariables]
    public EntityUid? Suit;

    [ViewVariables]
    public TimeSpan? ArriveAt;
}

public sealed partial class ImperialModPathfinderSummonEvent : InstantActionEvent;

/// <summary>stealth: модуль невидимости (видимость задаётся компонентом Stealth на носителе).</summary>
[RegisterComponent]
public sealed partial class ImperialModStealthModuleComponent : Component;

/// <summary>shooting_assistant: модуль хранит выбранный режим.</summary>
[RegisterComponent]
public sealed partial class ImperialModShootingModuleComponent : Component
{
    [DataField]
    public ImperialModShootingMode Mode;
}
