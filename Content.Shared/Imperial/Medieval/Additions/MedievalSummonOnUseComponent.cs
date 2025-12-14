using Robust.Shared.Prototypes;
using Robust.Shared.Audio;

namespace Content.Shared.Summoning;

[RegisterComponent]
public sealed partial class MedievalSummonOnUseComponent : Component
{
    [DataField("entitySummoned", required: true)]
    public EntProtoId EntityToSummon = string.Empty;

    [DataField("smokeEffect")]
    public EntProtoId SmokeEffect = "SummonSmoke";

    [DataField("summonDelay")]
    public TimeSpan SummonDelay = TimeSpan.FromSeconds(5);

    [DataField("sound")]
    public SoundSpecifier? UseSound;
}
