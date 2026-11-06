using System.Linq;
using System.Numerics;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.DoAfter;
using Content.Shared.Examine;
using Content.Shared.Hands;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Item;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Mobs.Components;
using Content.Shared.Physics;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Server.Imperial.Fishing;

/// <summary>
/// Источник с особой логикой (аквариум, портал и т. п.) сам выбирает награду (COMSIG_FISHING_CHALLENGE_ROLL_REWARD).
/// </summary>
[ByRefEvent]
public record struct ImperialFishingRollRewardEvent(ImperialFishingChallenge Challenge, ImperialFishingRodInfo Rod, string? RewardProto = null, EntityUid? RewardEntity = null);
