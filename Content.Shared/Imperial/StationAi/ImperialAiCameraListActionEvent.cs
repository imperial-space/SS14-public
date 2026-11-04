using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

// Кнопки HUD ИИ из TG (_onclick/hud/ai.dm), которых нет в ванильном SS14.

/// <summary>show_camera_list(): список камер станции.</summary>
public sealed partial class ImperialAiCameraListActionEvent : InstantActionEvent;
