using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent]
public sealed partial class SkeletonInvasionRuleComponent : Component
{
    [DataField]
    public (float, float) SpawnDelay = (12, 15);

    [DataField(required: true)]
    public ResPath Arena;

    [DataField]
    public List<string> SkullParts = new();

    [ViewVariables]
    public TimeSpan NextSpawn = TimeSpan.Zero;

    [ViewVariables]
    public int SpawnCount = 3;
}
