using Content.Shared.Body.Components;
using JetBrains.Annotations;
using Content.Server.SSDFree;
using Content.Server.SSDFree.Components;
using Content.Shared.SSDFree.Components;

namespace Content.Server.Destructible.Thresholds.Behaviors
{
    [DataDefinition]
    public sealed partial class GoSSDBehavior : IThresholdBehavior
    {
        public void Execute(EntityUid owner, DestructibleSystem system, EntityUid? cause = null)
        {
            var ssdsystem = system.EntityManager.System<SSDFreeSystem>();
            if (system.EntityManager.TryGetComponent(owner, out SSDFreeComponent? comp))
            {
                if (comp.CommonSession != null)
                {
                    var targetUser = comp.CommonSession.UserId;
                    ssdsystem.GoToSSD(comp.Owner, targetUser, false, comp);
                }
            }
        }
    }
}
