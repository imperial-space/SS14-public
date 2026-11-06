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
