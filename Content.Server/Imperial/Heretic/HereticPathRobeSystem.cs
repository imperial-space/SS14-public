using Content.Shared.Clothing;
using Content.Shared.Imperial.Heretic;
using Content.Shared.Imperial.Heretic.Components;

namespace Content.Server.Imperial.Heretic;

public sealed class HereticPathRobeSystem : EntitySystem
{
    [Dependency] private readonly HereticSystem             _heretic      = default!;
    [Dependency] private readonly HereticLockPassiveSystem  _lockPassive  = default!;
    [Dependency] private readonly HereticFleshPassiveSystem _fleshPassive = default!;
    [Dependency] private readonly HereticVoidPassiveSystem  _voidPassive  = default!;
    [Dependency] private readonly HereticRustPassiveSystem    _rustPassive    = default!;
    [Dependency] private readonly HereticCosmosPassiveSystem _cosmosPassive = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticPathRobeComponent, ClothingGotEquippedEvent>(OnEquipped);
    }

    private void OnEquipped(Entity<HereticPathRobeComponent> ent, ref ClothingGotEquippedEvent args)
    {
        if (!TryComp<HereticComponent>(args.Wearer, out var heretic))
            return;

        if (heretic.CurrentPath != ent.Comp.Path || heretic.PassiveLevel >= 2)
            return;

        _heretic.SetPassiveLevel(args.Wearer, heretic, 2);

        if (heretic.CurrentPath == HereticPath.Lock)
            _lockPassive.ApplyPassiveLevel2(args.Wearer);

        if (heretic.CurrentPath == HereticPath.Flesh)
            _fleshPassive.ApplyPassiveLevel2(args.Wearer);

        if (heretic.CurrentPath == HereticPath.Void)
            _voidPassive.ApplyPassiveLevel2(args.Wearer);

        if (heretic.CurrentPath == HereticPath.Rust)
            _rustPassive.ApplyPassiveLevel1(args.Wearer);

        if (heretic.CurrentPath == HereticPath.Cosmos)
            _cosmosPassive.ApplyPassiveLevel2(args.Wearer);
    }
}
