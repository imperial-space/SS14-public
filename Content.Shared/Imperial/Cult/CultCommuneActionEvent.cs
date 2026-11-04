using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Imperial.Cult;

// Совместимость: эти события используются неимперским Resources/Prototypes/Actions/cult.yml.
// Новый культ (1:1 с SS13) использует собственные действия из Imperial/Cult, а эти просто
// перенаправляются на эквивалентное поведение, если старое действие всё же выдано.

public sealed partial class CultCommuneActionEvent : InstantActionEvent;
