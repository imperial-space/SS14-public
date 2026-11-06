using Content.Shared.Buckle.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Ghost;
using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Imperial.ZLevels;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.StepTrigger.Systems;
using Content.Shared.Standing;
using Content.Shared.Stunnable;
using Content.Shared.Throwing;
using Robust.Server.Audio;
using Robust.Shared.Audio;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Сущность вот-вот упадёт на этаж ниже. Обработавший берёт падение на себя (парашют).</summary>
[ByRefEvent]
public record struct ZBeforeFallEvent(EntityUid Falling, MapCoordinates Target)
{
    public bool Handled;
}
