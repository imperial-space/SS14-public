using Content.Shared.Imperial.Heretic.Components;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Широковещательное событие: еретик вознёсся. Система каждого пути выдаёт свои способности вознесения.
/// </summary>
[ByRefEvent]
public readonly record struct HereticPathAscendedEvent(EntityUid Heretic, HereticComponent Component);

/// <summary>
/// Широковещательное событие: с сущности сняли компонент еретика. Системы путей убирают своё состояние.
/// </summary>
[ByRefEvent]
public readonly record struct HereticRemovedEvent(EntityUid Heretic);
