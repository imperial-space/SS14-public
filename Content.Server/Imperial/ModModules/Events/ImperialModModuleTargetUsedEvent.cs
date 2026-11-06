using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.ModModules;

/// <summary>Модуль применён по цели. Поднимается на модуле.</summary>
[ByRefEvent]
public record struct ImperialModModuleTargetUsedEvent(EntityUid Suit, EntityUid Wearer, EntityCoordinates Target, EntityUid? TargetEntity, bool Handled = false);
