using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Попытка каста: другие системы могут отменить (COMSIG_SPELL_CAN_CAST_CHECK / COMSIG_MOB_BEFORE_SPELL_CAST).</summary>
[ByRefEvent]
public record struct ImperialSpellCastAttemptEvent(EntityUid Caster, EntityUid Spell, bool Cancelled = false);
