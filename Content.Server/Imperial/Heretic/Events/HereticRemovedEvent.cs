using Content.Shared.Imperial.Heretic.Core;

namespace Content.Server.Imperial.Heretic;

/// <summary>
/// Широковещательное событие: с сущности сняли компонент еретика. Системы путей убирают своё состояние.
/// </summary>
[ByRefEvent]
public readonly record struct HereticRemovedEvent(EntityUid Heretic);
