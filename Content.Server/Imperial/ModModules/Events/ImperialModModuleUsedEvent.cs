using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.ModModules;

/// <summary>Модуль применён (MODULE_USABLE, on_use). Поднимается на модуле.</summary>
[ByRefEvent]
public record struct ImperialModModuleUsedEvent(EntityUid Suit, EntityUid Wearer, bool Handled = false);
