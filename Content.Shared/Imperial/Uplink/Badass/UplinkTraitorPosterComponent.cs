using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Imperial.Uplink.Badass;

/// <summary>Свёрнутый крамольный плакат (poster/traitor).</summary>
[RegisterComponent]
public sealed partial class UplinkTraitorPosterComponent : Component
{
    [DataField(required: true)]
    public List<EntProtoId> Posters = new();

    [DataField]
    public TimeSpan PosterAfterInteractDelay = TimeSpan.FromSeconds(2);
}
