using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.DeimonFly.DeathNote.Components;

/// <summary>
/// Видимый клиенту маркер торгового автомата, опрокинутого сценарием Тетради смерти.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
public sealed partial class DeathNoteFallenVendingVisualComponent : Component
{
    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public TimeSpan FallDuration;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float LiftScale = 1.18f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float ImpactHeightScale = 0.72f;

    [DataField, AutoNetworkedField, ViewVariables(VVAccess.ReadOnly)]
    public float ImpactProgress = 0.51f;
}
