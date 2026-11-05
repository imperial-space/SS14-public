using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Fishing;

/// <summary>Поймано (COMSIG_FISHING_ROD_CAUGHT_FISH), поднимается на удочке.</summary>
[ByRefEvent]
public readonly record struct ImperialFishingCaughtEvent(EntityUid User, EntityUid? Reward);
