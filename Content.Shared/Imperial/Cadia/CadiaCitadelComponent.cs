using Content.Shared.Dataset;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Cadia;

/// <summary>
/// Цитадель Касра: знамя, которое Хаос должен удержать в одиночку <see cref="CaptureTime"/>.
/// Защитники рядом останавливают захват и откатывают его.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CadiaCitadelComponent : Component
{
    [DataField]
    public float Radius = 6f;

    [DataField]
    public TimeSpan CaptureTime = TimeSpan.FromMinutes(3);

    [DataField, AutoNetworkedField]
    public float Progress;

    [DataField, AutoNetworkedField]
    public bool Captured;
}
