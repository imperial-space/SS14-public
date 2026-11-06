using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.StationAi;

/// <summary>Картинка ИИ поверх экрана (status_display/ai).</summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(raiseAfterAutoHandleState: true)]
public sealed partial class ImperialAiDisplayComponent : Component
{
    [DataField, AutoNetworkedField]
    public string? State;
}
