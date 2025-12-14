using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.SkeletonInvasion;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SkullBossStandComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), AutoNetworkedField]
    public Dictionary<int, bool> AttachedParts = new();

    public List<string> AttachedProtos = new();

    [DataField]
    public Dictionary<int, string> Announcements = new();

    [DataField]
    public SoundSpecifier? AttachSound;

    [DataField]
    public SoundSpecifier? CompleteSound;

    [DataField]
    public int RequiredParts = 8;
}
