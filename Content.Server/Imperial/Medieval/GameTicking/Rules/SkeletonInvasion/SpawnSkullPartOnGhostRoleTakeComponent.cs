using Robust.Shared.Audio;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.GameTicking.Rules;

[RegisterComponent]
public sealed partial class SpawnSkullPartOnGhostRoleTakeComponent : Component
{
    [DataField]
    public List<string> Prototypes = new();
}
