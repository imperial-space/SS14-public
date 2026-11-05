using Content.Shared.Imperial.Heretic.Core;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Широковещательное событие: еретик вознёсся. Система каждого пути выдаёт свои способности вознесения.
/// </summary>
[ByRefEvent]
public readonly record struct HereticPathAscendedEvent(EntityUid Heretic, HereticComponent Component);
