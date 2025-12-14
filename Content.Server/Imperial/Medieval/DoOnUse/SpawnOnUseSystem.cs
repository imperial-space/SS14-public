using Content.Shared.Coordinates;
using Content.Shared.Imperial.Medieval.DoOnUse.Spawn;
using Content.Shared.Interaction.Events;

namespace Content.Server.Imperial.Medieval.DoOnUse.Spawn;

public sealed partial class AddActionOnUseSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SpawnOnUseComponent, UseInHandEvent>(UseInHandEvent);
    }
    private void UseInHandEvent(EntityUid uid, SpawnOnUseComponent component, UseInHandEvent ev)
    {
        SpawnAtPosition(component.EntityPrototype, uid.ToCoordinates());
        QueueDel(uid);
    }
}
