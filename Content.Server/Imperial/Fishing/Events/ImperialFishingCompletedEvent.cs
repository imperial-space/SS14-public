using Robust.Shared.Random;
using System.Linq;
using Content.Shared.Imperial.Fishing;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.Imperial.Fishing;

/// <summary>Рыбалка закончилась (COMSIG_MOB_COMPLETE_FISHING). Источник с особой логикой может выдать награду сам.</summary>
[ByRefEvent]
public record struct ImperialFishingCompletedEvent(ImperialFishingChallenge Challenge, bool Win, bool Handled = false);
