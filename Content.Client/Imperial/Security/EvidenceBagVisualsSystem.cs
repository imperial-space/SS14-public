using Content.Shared.Imperial.Security;
using Robust.Client.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Security;

/// <summary>evidencebag/update_overlays(): внутри пакета видна иконка улики.</summary>
public sealed class EvidenceBagVisualsSystem : EntitySystem
{
    private const string ItemLayer = "item";

    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EvidenceBagComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<EvidenceBagComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInserted(Entity<EvidenceBagComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != EvidenceBagComponent.ContainerId || !TryComp<SpriteComponent>(ent, out var sprite))
            return;

        if (!_sprite.LayerMapTryGet((ent, sprite), ItemLayer, out var index, false))
            return;

        if (!TryComp<SpriteComponent>(args.Entity, out var itemSprite) || itemSprite.BaseRSI is not { } rsi ||
            itemSprite.AllLayers is not { } layers)
        {
            return;
        }

        foreach (var layer in layers)
        {
            if (!layer.Visible || layer.RsiState.Name is not { } state)
                continue;

            _sprite.LayerSetSprite((ent, sprite), index, new SpriteSpecifier.Rsi((layer.Rsi ?? rsi).Path, state));
            _sprite.LayerSetVisible((ent, sprite), index, true);
            return;
        }
    }

    private void OnRemoved(Entity<EvidenceBagComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != EvidenceBagComponent.ContainerId || args.Container.ContainedEntities.Count > 0)
            return;

        if (TryComp<SpriteComponent>(ent, out var sprite) && _sprite.LayerMapTryGet((ent, sprite), ItemLayer, out var index, false))
            _sprite.LayerSetVisible((ent, sprite), index, false);
    }
}
