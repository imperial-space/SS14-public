using System.Linq;
using Content.Client.Imperial.Medieval.CombatStance;
using Content.Shared.Construction.Prototypes;
using Content.Shared.Imperial.Medieval.Factions;
using Robust.Client.Placement;
using Robust.Client.Utility;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Medieval.CombatStance
{
    public sealed class StancePointHijack : PlacementHijack
    {
        public StanceUIController Controller;
        public FactionMemberGroup Group;
        public StancePointHijack(StanceUIController controller, FactionMemberGroup group)
        {
            Controller = controller;
            Group = group;
        }
        public override bool HijackPlacementRequest(EntityCoordinates coordinates)
        {
            return Controller.Placed(coordinates, Group);
        }

        /// <inheritdoc />
        public override bool HijackDeletion(EntityUid entity)
        {
            return true;
        }

        /// <inheritdoc />
        public override void StartHijack(PlacementManager manager)
        {
            base.StartHijack(manager);
            manager.CurrentTextures = new() { new SpriteSpecifier.Rsi(new("Imperial/Medieval/CombatStance/point.rsi"), "red").DirFrame0() };
        }
    }
}
