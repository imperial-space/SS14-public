using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Xenobiology;

/// <summary>datum/action/innate/slime/evolve.</summary>
public sealed partial class SlimeEvolveActionEvent : InstantActionEvent;

/// <summary>datum/action/innate/slime/reproduce.</summary>
public sealed partial class SlimeReproduceActionEvent : InstantActionEvent;

[Serializable, NetSerializable]
public enum SlimeVisualLayers : byte
{
    Base,
    Mood,
}

/// <summary>do_after зелья смены пола на разумном существе.</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeGenderChangeDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Взведённая граната-кластер слаймов (slimebang_active).</summary>
[Serializable, NetSerializable]
public enum SlimeGrenadeVisuals : byte
{
    Active,
}

// Действия консоли ксенобиологии (camera_advanced/xenobio).

/// <summary>Выход из камеры.</summary>
public sealed partial class XenobioCameraOffEvent : InstantActionEvent;

/// <summary>datum/action/innate/slime_place.</summary>
public sealed partial class XenobioSlimePlaceEvent : InstantActionEvent;

/// <summary>datum/action/innate/slime_pick_up.</summary>
public sealed partial class XenobioSlimePickUpEvent : InstantActionEvent;

/// <summary>datum/action/innate/feed_slime.</summary>
public sealed partial class XenobioFeedSlimeEvent : InstantActionEvent;

/// <summary>datum/action/innate/monkey_recycle.</summary>
public sealed partial class XenobioMonkeyRecycleEvent : InstantActionEvent;

/// <summary>datum/action/innate/slime_scan.</summary>
public sealed partial class XenobioSlimeScanEvent : InstantActionEvent;

/// <summary>datum/action/innate/feed_potion.</summary>
public sealed partial class XenobioFeedPotionEvent : InstantActionEvent;

/// <summary>datum/action/innate/hotkey_help.</summary>
public sealed partial class XenobioHotkeyHelpEvent : InstantActionEvent;

/// <summary>Анимация обработки в процессоре слаймов.</summary>
[Serializable, NetSerializable]
public enum SlimeProcessorVisuals : byte
{
    Processing,
}

/// <summary>do_after охлаждающего блюспейс экстракта (slimerecall).</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeCrossRecallDoAfterEvent : SimpleDoAfterEvent;

/// <summary>do_after кормления печеньем слайма другого существа.</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeCookieFeedDoAfterEvent : SimpleDoAfterEvent;

/// <summary>do_after зелий любви и мира.</summary>
[Serializable, NetSerializable]
public sealed partial class SlimeCrossPotionDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Промышленный экстракт работает (industrial / industrial_still).</summary>
[Serializable, NetSerializable]
public enum SlimeIndustrialVisuals : byte
{
    Working,
}

/// <summary>do_after взятия пробы тампоном или биопсией (3 секунды).</summary>
[Serializable, NetSerializable]
public sealed partial class VatSwabDoAfterEvent : SimpleDoAfterEvent;

/// <summary>Действие призматических очков.</summary>
public sealed partial class SlimePrismColourActionEvent : InstantActionEvent;

/// <summary>Действие призматических очков: призма света.</summary>
public sealed partial class SlimePrismPlaceActionEvent : InstantActionEvent;
