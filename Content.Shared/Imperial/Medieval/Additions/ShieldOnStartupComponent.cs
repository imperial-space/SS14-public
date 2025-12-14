using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Additions;

[RegisterComponent]
public sealed partial class ShieldOnStartupComponent : Component {
    public TimeSpan Spawned = TimeSpan.FromSeconds(0);
    [DataField]
    public bool Enabled = true;
}
