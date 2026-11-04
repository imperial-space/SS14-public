using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Xenomorph;

/// <summary>Гнездо: пристёгивают только ксеноморфы, самому выбраться — 100 с.</summary>
[RegisterComponent]
public sealed partial class XenoNestComponent : Component
{
    [DataField]
    public TimeSpan EscapeTime = TimeSpan.FromSeconds(100);
}
