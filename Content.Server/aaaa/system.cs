using Content.Shared.Coordinates;
using Content.Shared.Imperial.Spellward.Events;
using Robust.Server.GameObjects;
using Content.Shared.Imperial.Spellward;
using Content.Server.Actions;

namespace Content.Server.Imperial.Spellward;

public sealed partial class SpellwardTestSystems : EntitySystem
{
    [Dependency] private readonly ActionsSystem _actions = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpellwardTestComponent, SpellwardSpawnActionEvent>(OnUse);
        SubscribeLocalEvent<SpellwardTestComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<SpellwardTestComponent, ComponentShutdown>(OnComponentShutdown);
    }
    private void OnUse(EntityUid uid, SpellwardTestComponent comp, SpellwardSpawnActionEvent ev)
    {
        if (comp.IsFirst)
        {
            comp.Object = Spawn("MobHuman", uid.ToCoordinates());
            comp.IsFirst = false;
            return;
        }
        var entityManager = IoCManager.Resolve<IEntityManager>();

        var xformSystem = entityManager.System<TransformSystem>();

        var mapPosition = xformSystem.GetWorldPosition(uid);

        xformSystem.SetWorldPosition(
            comp.Object,
            mapPosition
        );
    }
    private void OnComponentInit(EntityUid uid, SpellwardTestComponent comp, ComponentInit ev)
    {
        _actions.AddAction(uid, ref comp.Action, comp.ActionId);
    }
    private void OnComponentShutdown(EntityUid uid, SpellwardTestComponent comp, ComponentShutdown ev)
    {
        _actions.RemoveAction(uid, comp.Action);
        QueueDel(comp.Object);
    }
}
