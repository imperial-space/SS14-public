using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Заклинание без цели (self/aoe/conjure): эффект задают компоненты на действии.</summary>
public sealed partial class ImperialSpellInstantEvent : InstantActionEvent;
