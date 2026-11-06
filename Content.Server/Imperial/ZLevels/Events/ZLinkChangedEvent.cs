using Content.Shared.Imperial.ZLevels;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Server.Imperial.ZLevels;

/// <summary>Связь коммуникаций между этажами поменялась (кто-то выше или ниже появился или пропал).</summary>
[ByRefEvent]
public readonly record struct ZLinkChangedEvent;
