using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Bola культа: проходит сквозь культистов.</summary>
[RegisterComponent]
public sealed partial class CultBolaComponent : Component
{
    [DataField]
    public TimeSpan BolaHitDelay = TimeSpan.FromMilliseconds(1);

    [DataField]
    public TimeSpan BolaPickupDelay = TimeSpan.FromMilliseconds(1);

    [DataField]
    public TimeSpan BolaPickupSeconds = TimeSpan.FromSeconds(6f);
}
