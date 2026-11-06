using Content.Shared.Actions;
using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.ModModules;

/// <summary>shooting_assistant: модуль хранит выбранный режим.</summary>
[RegisterComponent]
public sealed partial class ImperialModShootingModuleComponent : Component
{
    [DataField]
    public ImperialModShootingMode Mode;
}
