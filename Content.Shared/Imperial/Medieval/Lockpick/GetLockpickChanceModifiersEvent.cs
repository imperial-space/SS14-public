using Robust.Shared.GameStates;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared.MedievalLockpick.Components;

[ByRefEvent]
public record struct GetLockpickChanceModifiersEvent(float Modifier = 1f);
