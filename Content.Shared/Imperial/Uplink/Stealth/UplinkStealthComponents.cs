using Content.Shared.Roles;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Stealth;

/// <summary>Помада Синдиката (lipstick/syndie): после нанесения воздушные поцелуи становятся лазерными.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieLipstickComponent : Component
{
    [DataField]
    public EntProtoId Kisser = "ImperialSyndieKissBlower";
}

/// <summary>TRAIT_SYNDIE_KISS.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieKisserComponent : Component
{
    [DataField]
    public EntProtoId Kisser = "ImperialSyndieKissBlower";
}

/// <summary>Одноразовый «пистолет» воздушного поцелуя: исчезает после выстрела.</summary>
[RegisterComponent]
public sealed partial class UplinkSingleShotComponent : Component;

/// <summary>Глушитель TG (/obj/item/suppressor).</summary>
[RegisterComponent]
public sealed partial class UplinkSuppressorComponent : Component;

/// <summary>Оружие, на которое можно навинтить глушитель (can_suppress).</summary>
[RegisterComponent]
public sealed partial class UplinkSuppressibleComponent : Component
{
    [DataField]
    public string Slot = "suppressor";

    [ViewVariables]
    public Robust.Shared.Audio.SoundSpecifier? OriginalSound;
}

[Serializable, NetSerializable]
public enum UplinkSuppressorVisuals : byte
{
    Suppressed,
}

/// <summary>«Искусство оригами»: даёт TRAIT_PAPER_MASTER.</summary>
[RegisterComponent]
public sealed partial class UplinkOrigamiBookComponent : Component;

/// <summary>TRAIT_PAPER_MASTER: складывает из бумаги боевые самолётики.</summary>
[RegisterComponent]
public sealed partial class UplinkPaperMasterComponent : Component;

/// <summary>Бумажный самолётик: при попадании в глаза — размытие и паралич.</summary>
[RegisterComponent]
public sealed partial class UplinkPaperPlaneComponent : Component
{
    [DataField]
    public float HitProbability = 1f;
}

/// <summary>Суперострая лента: делает предмет застревающим при броске.</summary>
[RegisterComponent]
public sealed partial class UplinkPointyTapeComponent : Component
{
    /// <summary>Колющий урон при попадании обмотанным предметом (у липкой ленты TG — 0).</summary>
    [DataField]
    public float Damage = 10f;

    /// <summary>Время, за которое вонзившийся предмет можно вытащить.</summary>
    [DataField]
    public float RemovalTime = 3f;
}

/// <summary>Предмет обмотан острой лентой.</summary>
[RegisterComponent]
public sealed partial class UplinkPointyTapedComponent : Component
{
    [DataField]
    public float Damage = 10f;
}

/// <summary>Детектор ИИ (multitool/ai_detect).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkAiDetectorComponent : Component
{
    [DataField]
    public float RangeAlert = 8f;

    [DataField]
    public float RangeWarning = 20f;

    [DataField, AutoNetworkedField]
    public UplinkAiProximity State = UplinkAiProximity.None;

    [ViewVariables]
    public TimeSpan NextCheck;
}

[Serializable, NetSerializable]
public enum UplinkAiProximity : byte
{
    None,
    Near,
    OnScreen,
}

[Serializable, NetSerializable]
public enum UplinkAiDetectorVisuals : byte
{
    State,
}

/// <summary>ЭМИ-фонарик (flashlight/emp).</summary>
[RegisterComponent]
public sealed partial class UplinkEmpFlashlightComponent : Component
{
    [DataField]
    public int MaxCharges = 4;

    [DataField]
    public int Charges = 4;

    [DataField]
    public TimeSpan ChargeDelay = TimeSpan.FromSeconds(20);

    [ViewVariables]
    public TimeSpan NextCharge;
}

/// <summary>Мануал кодовой речи.</summary>
[RegisterComponent]
public sealed partial class UplinkCodespeakManualComponent : Component
{
    /// <summary>Сколько раз можно научить; -1 — бесконечно (deluxe).</summary>
    [DataField]
    public int Charges = -1;
}

/// <summary>Знает кодовую речь Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkCodespeakerComponent : Component;

/// <summary>Набор поддельной личности (fake_identity_kit).</summary>
[RegisterComponent]
public sealed partial class UplinkFakeIdentityKitComponent : Component
{
    [DataField]
    public ProtoId<JobPrototype> Job = "Passenger";

    [DataField]
    public EntProtoId IdCard = "PassengerIDCard";
}

/// <summary>Шприц «Маллиган»: полностью меняет внешность и имя.</summary>
[RegisterComponent]
public sealed partial class UplinkMulliganComponent : Component;

/// <summary>Подделыватель криминалистики (forensics_spoofer).</summary>
[RegisterComponent]
public sealed partial class UplinkForensicsSpooferComponent : Component
{
    [ViewVariables]
    public HashSet<string> Fingerprints = new();

    [ViewVariables]
    public HashSet<string> Fibers = new();

    /// <summary>true — режим сканирования, false — нанесения.</summary>
    [ViewVariables]
    public bool ScanMode = true;
}

/// <summary>Том Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndieTomeComponent : Component
{
    [DataField]
    public int Uses = 1;

    [ViewVariables]
    public string? OwnerName;
}

/// <summary>Контактные линзы Синдиката: защита от вспышек.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicateContactsComponent : Component;

/// <summary>Глаза носителя защищены линзами (flash_protect).</summary>
[RegisterComponent]
public sealed partial class UplinkFlashProtectedComponent : Component;

/// <summary>Гипнотическая вспышка.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnoticFlashComponent : Component;

/// <summary>Гипнотическая граната: все в радиусе впадают в транс.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnoticGrenadeComponent : Component
{
    [DataField]
    public float Range = 7f;
}

/// <summary>Транс: следующая услышанная фраза станет гипнотическим внушением.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnoticTranceComponent : Component
{
    [ViewVariables]
    public TimeSpan EndTime;
}

/// <summary>Гипноз (brain trauma hypnosis): навязчивая фраза.</summary>
[RegisterComponent]
public sealed partial class UplinkHypnotizedComponent : Component
{
    [ViewVariables]
    public string Phrase = string.Empty;

    [ViewVariables]
    public TimeSpan NextReminder;
}

/// <summary>Радиоактивный микролазер.</summary>
[RegisterComponent]
public sealed partial class UplinkRadLaserComponent : Component
{
    [DataField]
    public int Intensity = 10;

    [DataField]
    public int Wavelength = 10;

    [ViewVariables]
    public TimeSpan ReadyAt;
}

/// <summary>Отложенный эффект микролазера на цели.</summary>
[RegisterComponent]
public sealed partial class UplinkRadLaserVictimComponent : Component
{
    [ViewVariables]
    public TimeSpan TriggerAt;

    [ViewVariables]
    public int Intensity;
}

/// <summary>Экспериментальный телепортер Синдиката.</summary>
[RegisterComponent]
public sealed partial class UplinkSyndicateTeleporterComponent : Component
{
    [DataField]
    public int Charges = 4;

    [DataField]
    public int MaxCharges = 4;

    [DataField]
    public int MinDistance = 4;

    [DataField]
    public int MaxDistance = 8;

    [DataField]
    public int ParallelDistance = 3;

    [ViewVariables]
    public TimeSpan NextRechargeRoll;
}

/// <summary>Взломанный модуль законов ИИ.</summary>
[RegisterComponent]
public sealed partial class UplinkHackedLawModuleComponent : Component
{
    [DataField]
    public string Law = string.Empty;
}

/// <summary>Картонная фигура: смена облика мелком.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class UplinkCardboardCutoutComponent : Component
{
    [DataField]
    public List<string> States = new();

    [DataField, AutoNetworkedField]
    public int Index;
}

[Serializable, NetSerializable]
public enum UplinkCutoutVisuals : byte
{
    State,
}

/// <summary>Диск F.R.A.M.E.: превращает чужой КПК в аплинк без ТК.</summary>
[RegisterComponent]
public sealed partial class UplinkFrameDiskComponent : Component
{
    [DataField]
    public int Charges = 5;
}

/// <summary>Аварийный код самоуничтожения аплинка (failsafe).</summary>
[RegisterComponent]
public sealed partial class UplinkFailsafeComponent : Component
{
    [ViewVariables]
    public Content.Shared.PDA.Note[]? Code;
}

/// <summary>Телефон «Протокол CRAB-17».</summary>
[RegisterComponent]
public sealed partial class UplinkCrabPhoneComponent : Component
{
    [DataField]
    public EntProtoId Machine = "ImperialCrab17Machine";
}

/// <summary>Машина CRAB-17: выкачивает деньги станции, пока работает.</summary>
[RegisterComponent]
public sealed partial class UplinkCrabMachineComponent : Component
{
    [DataField]
    public TimeSpan Duration = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <summary>Доля средств, уходящая за один цикл.</summary>
    [DataField]
    public float Fraction = 0.05f;

    [ViewVariables]
    public int Stolen;

    [ViewVariables]
    public TimeSpan EndTime;

    [ViewVariables]
    public TimeSpan NextDrain;

    [ViewVariables]
    public EntityUid? Owner;
}

/// <summary>Телепад в кейсе.</summary>
[RegisterComponent]
public sealed partial class UplinkLaunchpadComponent : Component
{
    [DataField]
    public int Range = 8;

    [DataField]
    public EntProtoId? Briefcase = "ImperialUplinkLaunchpadBriefcase";
}

/// <summary>Кейс, раскладывающийся в телепад.</summary>
[RegisterComponent]
public sealed partial class UplinkLaunchpadBriefcaseComponent : Component
{
    [DataField]
    public EntProtoId Pad = "ImperialUplinkLaunchpad";
}

/// <summary>Пульт телепада, замаскированный под папку.</summary>
[RegisterComponent]
public sealed partial class UplinkLaunchpadRemoteComponent : Component
{
    [ViewVariables]
    public EntityUid? Pad;

    [ViewVariables]
    public EntityUid? Briefcase;

    [DataField]
    public int X;

    [DataField]
    public int Y = 1;

    [ViewVariables]
    public TimeSpan ReadyAt;
}


[Serializable, NetSerializable]
public sealed partial class UplinkContactsDoAfterEvent : Content.Shared.DoAfter.SimpleDoAfterEvent;

[Serializable, NetSerializable]
public enum UplinkLaunchpadUiKey : byte
{
    Key,
}

/// <summary>Окно пульта телепада (launchpad_remote TG).</summary>
[Serializable, NetSerializable]
public sealed class UplinkLaunchpadState(bool linked, bool deployed, int x, int y, int range) : BoundUserInterfaceState
{
    public readonly bool Linked = linked;
    public readonly bool Deployed = deployed;
    public readonly int X = x;
    public readonly int Y = y;
    public readonly int Range = range;
}

[Serializable, NetSerializable]
public sealed class UplinkLaunchpadOffsetMessage(int x, int y) : BoundUserInterfaceMessage
{
    public readonly int X = x;
    public readonly int Y = y;
}

[Serializable, NetSerializable]
public sealed class UplinkLaunchpadFireMessage(bool sending) : BoundUserInterfaceMessage
{
    public readonly bool Sending = sending;
}
