using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Imperial.Lavaland.OrePoints.Mining.Components;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Lavaland.OrePoints.Mining;

/// <summary>
/// Визуал трофеев дробителя: ледяная глыба ice_block_talisman и облик черепа retool_kit/ashenskull.
/// </summary>
public sealed class CrusherTrophyVisualsSystem : EntitySystem
{
    [Dependency] private readonly IComponentFactory _factory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly SpriteSpecifier.Rsi IceCube = new(new ResPath("/Textures/Imperial/lava/mining/crusher_effects.rsi"), "ice_cube");
    private static readonly EntProtoId SkullTemplate = "CrusherSkullSkinTemplate";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CrusherFrozenComponent, ComponentStartup>(OnFrozenStartup);
        SubscribeLocalEvent<CrusherFrozenComponent, ComponentShutdown>(OnFrozenShutdown);
        SubscribeLocalEvent<KineticCrusherComponent, AppearanceChangeEvent>(OnCrusherAppearance);
    }

    private void OnFrozenStartup(Entity<CrusherFrozenComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var layer = _sprite.LayerMapReserve((ent, sprite), CrusherFrozenKey.Key);
        _sprite.LayerSetRsi((ent, sprite), layer, IceCube.RsiPath, IceCube.RsiState);
    }

    private void OnFrozenShutdown(Entity<CrusherFrozenComponent> ent, ref ComponentShutdown args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || !_sprite.LayerMapTryGet((ent, sprite), CrusherFrozenKey.Key, out var layer, false))
            return;

        _sprite.RemoveLayer((ent, sprite), layer);
    }

    /// <summary>update_reskin(): меняет спрайт, спрайт в руках и на спине.</summary>
    private void OnCrusherAppearance(Entity<KineticCrusherComponent> ent, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        var skull = args.AppearanceData.TryGetValue(CrusherVisuals.Skull, out var value) && value is true;
        var source = skull ? SkullTemplate.Id : MetaData(ent).EntityPrototype?.ID;
        if (source == null || !_proto.TryIndex<EntityPrototype>(source, out var proto))
            return;

        if (proto.TryGetComponent<SpriteComponent>(out var protoSprite, _factory) && protoSprite.BaseRSI != null)
            _sprite.LayerSetRsi((ent, args.Sprite), 0, protoSprite.BaseRSI.Path, "icon");

        if (proto.TryGetComponent<ItemComponent>(out var item, _factory))
            _item.CopyVisuals(ent, item);

        if (proto.TryGetComponent<ClothingComponent>(out var clothing, _factory))
            _clothing.CopyVisuals(ent, clothing);
    }

    private enum CrusherFrozenKey : byte
    {
        Key,
    }
}
