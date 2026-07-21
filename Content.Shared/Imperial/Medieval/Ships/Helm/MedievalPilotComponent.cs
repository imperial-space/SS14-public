using Robust.Shared.Audio;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Medieval.Ships.Helm;

[RegisterComponent]
public sealed partial class MedievalPilotComponent : Component
{
    [DataField]
    public EntityUid? HelmEntity;

    [DataField]
    public float Turning;

    [DataField]
    public EntityUid? UsingSound;
}
