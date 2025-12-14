using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Plague;

[RegisterComponent, NetworkedComponent]
public sealed partial class MedievalPlagueImmuneComponent : Component
{
    public TimeSpan StartTime;

    public bool HardImmunity = false;
}
