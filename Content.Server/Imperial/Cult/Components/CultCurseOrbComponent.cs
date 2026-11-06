using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Cursed orb (shuttle_curse).</summary>
[RegisterComponent]
public sealed partial class CultCurseOrbComponent : Component
{
    [DataField]
    public TimeSpan CurseOrbDelay = TimeSpan.FromMinutes(3);

    [DataField]
    public TimeSpan CurseOrbDelay2 = TimeSpan.FromSeconds(10);

    [DataField]
    public TimeSpan CurseOrbSeconds = TimeSpan.FromSeconds(10f);
}
