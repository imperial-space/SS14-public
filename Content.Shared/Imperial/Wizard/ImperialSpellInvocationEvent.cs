using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>COMSIG_MOB_PRE_INVOCATION: можно подменить фразу и тип произнесения.</summary>
[ByRefEvent]
public record struct ImperialSpellInvocationEvent(EntityUid Spell, string? Message, ImperialInvocationType Type);
