using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Heretic.Items;

[RegisterComponent, AutoGenerateComponentPause, NetworkedComponent]
public sealed partial class HereticLivingHeartComponent : Component
{
    [DataField]
    public EntityUid? Target;

    [ViewVariables, AutoPausedField]
    public TimeSpan NextHeartbeat;

    [DataField]
    public TimeSpan MinInterval = TimeSpan.FromSeconds(0.35f);

    [DataField]
    public TimeSpan MaxInterval = TimeSpan.FromSeconds(3.0f);

    [DataField]
    public float TrackRange = 30f;

    [DataField]
    public int SacrificeAtRuneAmount = 4;

    [DataField]
    public int SacrificeAtRuneAmount2 = 2;
}
