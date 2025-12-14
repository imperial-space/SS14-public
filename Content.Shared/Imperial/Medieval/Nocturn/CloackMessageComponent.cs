using Robust.Shared.GameStates;
using Content.Shared.Actions;

namespace Content.Shared.Imperial.Medieval.Factions.Components
{

    public sealed partial class CloackMessageActionEvent : InstantActionEvent { }

    [RegisterComponent, NetworkedComponent]
    public sealed partial class CloackMessageComponent : Component
    {
        [DataField]
        public string Action = "";

        [DataField]
        public string Faction = "none";
    }
}
