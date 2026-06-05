using Robust.Shared.Audio;

namespace Content.Server.Imperial.Lavaland.BossMusic;

[RegisterComponent]
public sealed partial class MegafaunaBossMusicComponent : Component
{
    [DataField(required: true)]
    public SoundSpecifier Music = default!;

    [DataField]
    public float Range = 30f;

    public Dictionary<EntityUid, EntityUid> ActiveStreams = new();
}
