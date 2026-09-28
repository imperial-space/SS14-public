namespace Content.Server.Imperial.Heretic;

[RegisterComponent]
public sealed partial class HereticSpacePhaseActiveComponent : Component
{
    // Tracks whether SpacePhase granted pressure immunity (to avoid removing Creator's Gift permanent immunity)
    public bool GrantedPressureImmunity = false;
}
