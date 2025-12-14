using Robust.Shared.GameStates;

namespace Content.Shared.Imperial.Medieval.Factions.Components
{

    [RegisterComponent, NetworkedComponent]
    public sealed partial class CloackRecieverComponent : Component
    {
        [DataField]
        public string Faction = "none";
    }
}
