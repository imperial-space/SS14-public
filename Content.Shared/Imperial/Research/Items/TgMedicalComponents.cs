using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Research.Items;

[Serializable, NetSerializable]
public enum ImperialMedScanMode : byte
{
    /// <summary>autopsy_scanner: отчёт о вскрытии трупа на бумаге.</summary>
    Autopsy,
    /// <summary>ph_meter: состав раствора в ёмкости.</summary>
    Chemical,
    /// <summary>blood_scanner: уровень крови и лекарства в ней.</summary>
    Blood,
    /// <summary>penlight: реакция зрачков.</summary>
    Pupils,
    /// <summary>reflexhammer: рефлексы и неврологический урон.</summary>
    Reflex,
}

/// <summary>Медицинский диагностический прибор TG: результат выводится всплывающим сообщением или бумагой.</summary>
[RegisterComponent]
public sealed partial class ImperialMedScannerComponent : Component
{
    [DataField(required: true)]
    public ImperialMedScanMode Mode;

    [DataField]
    public float Delay = 1f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialMedScanDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Медицинский инструмент TG, действующий на пациента после задержки.</summary>
[RegisterComponent]
public sealed partial class ImperialMedToolComponent : Component
{
    [DataField]
    public float Delay = 5f;

    /// <summary>blood_filter: сколько единиц чужих реагентов убрать из крови.</summary>
    [DataField]
    public FixedPoint2 FilterAmount;

    /// <summary>bonesetter: сколько тупого урона лечит одно вправление.</summary>
    [DataField]
    public float HealBlunt;
}

[Serializable, NetSerializable]
public sealed partial class ImperialMedToolDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Капельница-пакет (chem_pack): подсоединяется к пациенту и вливает раствор.</summary>
[RegisterComponent]
public sealed partial class ImperialIvBagComponent : Component
{
    [DataField]
    public string Solution = "pack";

    [DataField]
    public FixedPoint2 Rate = 2;

    [DataField]
    public float MaxDistance = 1.5f;

    [ViewVariables]
    public EntityUid? Patient;

    [ViewVariables]
    public TimeSpan NextDrip;
}

/// <summary>Компьютер или дисплей, показывающий состояние пациента рядом (operating, vitals display).</summary>
[RegisterComponent]
public sealed partial class ImperialVitalsDisplayComponent : Component
{
    [DataField]
    public float Range = 2f;

    /// <summary>Подробный отчёт (продвинутый дисплей, операционный компьютер).</summary>
    [DataField]
    public bool Advanced;
}

/// <summary>Машина пополнения медипенов (medipen_refiller).</summary>
[RegisterComponent]
public sealed partial class ImperialMedipenRefillerComponent : Component
{
    [DataField]
    public string Solution = "tank";

    /// <summary>Сколько энергии стоит одна заправка — время в секундах.</summary>
    [DataField]
    public float RefillTime = 2f;
}

[Serializable, NetSerializable]
public sealed partial class ImperialMedipenRefillDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Хроматограф ВЭЖХ (chem_mass_spec): переносит выбранный реагент из левого стакана в правый.</summary>
[RegisterComponent]
public sealed partial class ImperialChromatographComponent : Component
{
    [DataField]
    public string InputSlot = "beakerIn";

    [DataField]
    public string OutputSlot = "beakerOut";
}

/// <summary>Пинпоинтер экипажа (pinpointer/crew): выбор цели из членов экипажа с включёнными датчиками.</summary>
[RegisterComponent]
public sealed partial class ImperialCrewPinpointerComponent : Component;

/// <summary>Дым-машина (smoke_machine): выпускает облака дыма из своего раствора.</summary>
[RegisterComponent]
public sealed partial class ImperialSmokeMachineComponent : Component
{
    [DataField]
    public string Solution = "tank";

    [DataField]
    public bool Enabled;

    [DataField]
    public FixedPoint2 PerPuff = 10;

    [DataField]
    public int Spread = 8;

    [DataField]
    public float Interval = 5f;

    [ViewVariables]
    public TimeSpan NextPuff;
}

/// <summary>Стазисный мешок для тел: замедляет метаболизм содержимого.</summary>
[RegisterComponent]
public sealed partial class ImperialStasisBagComponent : Component
{
    [DataField]
    public float Multiplier = 10f;
}

/// <summary>Тело внутри стазисного мешка.</summary>
[RegisterComponent]
public sealed partial class ImperialInStasisBagComponent : Component
{
    [ViewVariables]
    public float Multiplier = 10f;
}
