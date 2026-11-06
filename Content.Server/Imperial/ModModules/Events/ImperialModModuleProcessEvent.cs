using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.ModModules;

/// <summary>Модуль активен, тик раз в секунду (on_active_process). Поднимается на модуле.</summary>
[ByRefEvent]
public readonly record struct ImperialModModuleProcessEvent(EntityUid Suit, EntityUid Wearer, float FrameTime);
