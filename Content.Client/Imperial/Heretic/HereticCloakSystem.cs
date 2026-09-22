using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCloakSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCloakActiveComponent, ComponentStartup>(OnCloakAdded);
        SubscribeLocalEvent<HereticCloakActiveComponent, ComponentShutdown>(OnCloakRemoved);
    }

    private void OnCloakAdded(EntityUid uid, HereticCloakActiveComponent _, ComponentStartup args)
    {
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.Visible = false;
    }

    private void OnCloakRemoved(EntityUid uid, HereticCloakActiveComponent _, ComponentShutdown args)
    {
        if (TerminatingOrDeleted(uid))
            return;
        if (TryComp<SpriteComponent>(uid, out var sprite))
            sprite.Visible = true;
    }
}
