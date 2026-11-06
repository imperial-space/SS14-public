using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Wizard;

/// <summary>Заклинание «Телепорт» (area_teleport/wizard): выбор зоны станции.</summary>
public sealed partial class ImperialAreaTeleportActionEvent : InstantActionEvent;
