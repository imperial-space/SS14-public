using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Рунная дверь: оглушает не-культистов.</summary>
[RegisterComponent]
public sealed partial class CultRunedDoorComponent : Component
{
    [DataField] public bool Runed = true;

    /// <summary>friendly: открывается всем.</summary>
    [DataField] public bool Friendly;

    [ViewVariables] public bool Concealed;

    [DataField]
    public TimeSpan DoorCooldown = TimeSpan.FromSeconds(1);

    [DataField]
    public TimeSpan DoorOpeningSeconds = TimeSpan.FromSeconds(4f);

    [DataField]
    public float DoorOpeningBaseThrowSpeed = 10f;
}
