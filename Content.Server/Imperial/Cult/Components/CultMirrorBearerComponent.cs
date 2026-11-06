using Content.Shared.Imperial.Cult;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Server.Imperial.Cult.Components;

/// <summary>Некультист держит зеркальный щит: щит предаёт его (иллюзии-двойники).</summary>
[RegisterComponent]
public sealed partial class CultMirrorBearerComponent : Component
{
    [ViewVariables] public EntityUid? Shield;

    [DataField]
    public float BearerDamageChance = 0.5f;
}
