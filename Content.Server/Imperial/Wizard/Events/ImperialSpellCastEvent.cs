using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Server.Imperial.Wizard;

/// <summary>Заклинание прочитано (COMSIG_MOB_CAST_SPELL): поднимается на заклинателе.</summary>
[ByRefEvent]
public readonly record struct ImperialSpellCastEvent(EntityUid Caster, EntityUid Spell);
