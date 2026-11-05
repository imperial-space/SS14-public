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
