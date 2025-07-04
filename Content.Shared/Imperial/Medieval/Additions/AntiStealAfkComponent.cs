using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Additions;

[RegisterComponent]
public sealed partial class AntiStealAfkComponent : Component {
    public TimeSpan Leaved = TimeSpan.FromSeconds(0);
}
