using Content.Shared.Actions;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Shared.Imperial.StationAi;

/// <summary>/datum/ai_module/malf: модуль сбойного ИИ, покупается за процессорное время.</summary>
[Prototype]
public sealed partial class ImperialMalfModulePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public LocId Description;

    [DataField(required: true)]
    public int Cost;

    /// <summary>Destructive Modules / Utility Modules / Upgrade Modules.</summary>
    [DataField(required: true)]
    public ImperialMalfModuleCategory Category;

    /// <summary>one_purchase.</summary>
    [DataField]
    public bool OnePurchase;

    /// <summary>minimum_apcs.</summary>
    [DataField]
    public int MinimumApcs;

    [DataField]
    public SpriteSpecifier? Icon;

    /// <summary>power_type: действие, которое выдаёт модуль.</summary>
    [DataField]
    public EntProtoId? Action;

    /// <summary>uses: сколько применений даёт одна покупка (0 — без ограничения).</summary>
    [DataField]
    public int Uses;

    [DataField]
    public LocId? UnlockText;

    [DataField]
    public SoundSpecifier? UnlockSound;

    [DataField]
    public int Order;
}

[Serializable, NetSerializable]
public enum ImperialMalfModuleCategory : byte
{
    Destructive,
    Utility,
    Upgrade,
}

/// <summary>Открыть меню модулей сбоя.</summary>
public sealed partial class ImperialMalfModulesActionEvent : InstantActionEvent;

/// <summary>Взломать ЛКП (malfhack): цель — ЛКП.</summary>
public sealed partial class ImperialMalfHackApcActionEvent : WorldTargetActionEvent;

/// <summary>Сервер → клиент: меню модулей (процессорное время, взломанные ЛКП, купленные разовые модули).</summary>
[Serializable, NetSerializable]
public sealed class ImperialMalfPickerEvent(int processingTime, int hackedApcs, List<string> bought, Dictionary<string, int> costs, bool open) : EntityEventArgs
{
    public int ProcessingTime = processingTime;
    public int HackedApcs = hackedApcs;
    public List<string> Bought = bought;

    /// <summary>Цены со скидкой (устройство Судного дня).</summary>
    public Dictionary<string, int> Costs = costs;

    /// <summary>false — только обновить уже открытое окно.</summary>
    public bool Open = open;
}

/// <summary>Клиент → сервер: купить модуль.</summary>
[Serializable, NetSerializable]
public sealed class ImperialMalfBuyEvent(string module) : EntityEventArgs
{
    public string Module = module;
}

#region Действия модулей

/// <summary>lockdown.</summary>
public sealed partial class ImperialMalfLockdownActionEvent : InstantActionEvent;

/// <summary>override_machine.</summary>
public sealed partial class ImperialMalfOverrideMachineActionEvent : WorldTargetActionEvent;

/// <summary>overload_machine.</summary>
public sealed partial class ImperialMalfOverloadMachineActionEvent : WorldTargetActionEvent;

/// <summary>destroy_rcds.</summary>
public sealed partial class ImperialMalfDestroyRcdsActionEvent : InstantActionEvent;

/// <summary>blackout.</summary>
public sealed partial class ImperialMalfBlackoutActionEvent : InstantActionEvent;

/// <summary>megahonk.</summary>
public sealed partial class ImperialMalfHonkActionEvent : InstantActionEvent;

/// <summary>place_cyborg_transformer.</summary>
public sealed partial class ImperialMalfFactoryActionEvent : InstantActionEvent;

/// <summary>break_air_alarms (режим затопления одной тревоги).</summary>
public sealed partial class ImperialMalfFloodActionEvent : WorldTargetActionEvent;

/// <summary>break_fire_alarms.</summary>
public sealed partial class ImperialMalfFireAlarmsActionEvent : InstantActionEvent;

/// <summary>emergency_lights.</summary>
public sealed partial class ImperialMalfEmergencyLightsActionEvent : InstantActionEvent;

/// <summary>reactivate_cameras.</summary>
public sealed partial class ImperialMalfReactivateCamerasActionEvent : InstantActionEvent;

/// <summary>remote_emag.</summary>
public sealed partial class ImperialMalfEmagActionEvent : WorldTargetActionEvent;

/// <summary>core_tilt.</summary>
public sealed partial class ImperialMalfRollActionEvent : WorldTargetActionEvent;

/// <summary>remote_vendor_tilt.</summary>
public sealed partial class ImperialMalfVendorTiltActionEvent : WorldTargetActionEvent;

/// <summary>mecha_domination.</summary>
public sealed partial class ImperialMalfDominateMechActionEvent : WorldTargetActionEvent;

/// <summary>Вернуться из меха в ядро.</summary>
public sealed partial class ImperialMalfMechReturnActionEvent : InstantActionEvent;

/// <summary>voice_changer.</summary>
public sealed partial class ImperialMalfVoiceChangerActionEvent : InstantActionEvent;

/// <summary>nuke_station: устройство Судного дня.</summary>
public sealed partial class ImperialMalfDoomsdayActionEvent : InstantActionEvent;

/// <summary>Перенос ядра в взломанный ЛКП (shunt).</summary>
public sealed partial class ImperialMalfShuntActionEvent : WorldTargetActionEvent;

/// <summary>Вернуться из ЛКП в ядро (return_to_core).</summary>
public sealed partial class ImperialMalfReturnToCoreActionEvent : InstantActionEvent;

#endregion

/// <summary>mimic/copy/machine: оживлённая машина рисуется спрайтом своего прототипа.</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class ImperialMalfMimicComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? Proto;
}
