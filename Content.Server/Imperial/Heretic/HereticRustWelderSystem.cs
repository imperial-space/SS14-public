using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Tools.Components;
using Robust.Shared.GameObjects;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticRustWelderSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem _heretic = default!;
    [Dependency] private readonly ItemToggleSystem _itemToggle = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<WelderComponent, AfterInteractEvent>(OnWelderAfterInteract);
    }

    private void OnWelderAfterInteract(Entity<WelderComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Target != null)
            return;
        if (!_itemToggle.IsActivated(ent.Owner))
            return;
        if (!_heretic.IsTileRusted(args.ClickLocation))
            return;

        _heretic.UnrustTile(args.ClickLocation);
        args.Handled = true;
    }
}
