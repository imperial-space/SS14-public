using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.Megafauna;

/// <summary>GiveTarget / LoseTarget.</summary>
[ByRefEvent]
public record struct MegafaunaTargetChangedEvent(EntityUid? OldTarget, EntityUid? NewTarget);
