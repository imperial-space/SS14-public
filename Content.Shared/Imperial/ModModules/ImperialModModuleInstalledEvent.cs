using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>Модуль установлен в костюм или извлечён из него (on_install / on_uninstall). Поднимается на модуле.</summary>
[ByRefEvent]
public readonly record struct ImperialModModuleInstalledEvent(EntityUid Suit, bool Installed);
