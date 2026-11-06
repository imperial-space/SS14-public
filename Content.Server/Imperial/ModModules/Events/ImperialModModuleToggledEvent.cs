using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.ModModules;

/// <summary>Модуль включён или выключен (on_activation / on_deactivation). Поднимается на модуле.</summary>
[ByRefEvent]
public readonly record struct ImperialModModuleToggledEvent(EntityUid Suit, EntityUid? Wearer, bool Active);
