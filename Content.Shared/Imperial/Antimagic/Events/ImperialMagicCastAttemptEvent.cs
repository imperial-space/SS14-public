using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Antimagic;

/// <summary>
/// Поднимается на заклинателе перед применением заклинания с <see cref="ImperialMagicActionComponent"/>.
/// Антагонист может отменить своё колдовство, например еретик со святой водой в теле.
/// </summary>
[ByRefEvent]
public record struct ImperialMagicCastAttemptEvent(EntityUid User, bool Cancelled = false);
