using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.Megafauna;

/// <summary>celebrate_kill: босс выпотрошил жертву.</summary>
[ByRefEvent]
public record struct MegafaunaDevourEvent(EntityUid Victim);
