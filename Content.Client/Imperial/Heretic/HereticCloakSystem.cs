using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticCloakSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticCloakActiveComponent, ComponentStartup>(OnCloakAdded);
        SubscribeLocalEvent<HereticCloakActiveComponent, ComponentShutdown>(OnCloakRemoved);
    }

    private void OnCloakAdded(Entity<HereticCloakActiveComponent> ent, ref ComponentStartup args)
    {
        _sprite.SetVisible(ent.Owner, false);
    }

    private void OnCloakRemoved(Entity<HereticCloakActiveComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _sprite.SetVisible(ent.Owner, true);
    }
}
