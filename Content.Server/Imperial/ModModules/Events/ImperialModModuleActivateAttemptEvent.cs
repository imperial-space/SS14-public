using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.ModModules;

/// <summary>Проверка, можно ли включить модуль (COMSIG_MODULE_TRIGGERED). Поднимается на модуле.</summary>
[ByRefEvent]
public record struct ImperialModModuleActivateAttemptEvent(EntityUid Suit, EntityUid Wearer, bool Cancelled = false);
