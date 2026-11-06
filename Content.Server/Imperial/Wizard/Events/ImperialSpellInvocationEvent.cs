using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

using Content.Shared.Imperial.Wizard;

namespace Content.Server.Imperial.Wizard;

/// <summary>COMSIG_MOB_PRE_INVOCATION: можно подменить фразу и тип произнесения.</summary>
[ByRefEvent]
public record struct ImperialSpellInvocationEvent(EntityUid Spell, string? Message, ImperialInvocationType Type);
