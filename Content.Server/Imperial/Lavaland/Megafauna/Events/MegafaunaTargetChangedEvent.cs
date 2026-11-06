using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.Megafauna;

/// <summary>GiveTarget / LoseTarget.</summary>
[ByRefEvent]
public readonly record struct MegafaunaTargetChangedEvent(EntityUid? OldTarget, EntityUid? NewTarget);
