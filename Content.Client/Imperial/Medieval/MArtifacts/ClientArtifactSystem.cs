using Content.Client.Items.Systems;
using Content.Shared.Hands;
using Content.Shared.Imperial.Medieval.Artifacts;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.GameStates;
using Robust.Shared.Utility;

namespace Content.Server.Imperial.Medieval.Artifacts;

public sealed class ArtifactSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly IResourceCache _rescache = default!;
    [Dependency] private readonly ItemSystem _item = default!;
    public override void Initialize()
    {
        SubscribeLocalEvent<ArtifactComponent, ComponentHandleState>(State);
    }
    private void State(EntityUid uid, ArtifactComponent component, ref ComponentHandleState args)
    {
        if (args.Current is not ArtifactSpriteState state)
            return;
        var item = EnsureComp<ItemComponent>(uid);
        item.RsiPath = $"{state.Path}/inhand.rsi";
        if (_rescache.TryGetResource($"{state.Path}/icon.rsi", out RSIResource? rsi))
            _sprite.SetBaseRsi((uid, Comp<SpriteComponent>(uid)), rsi.RSI);
        _item.VisualsChanged(uid);
    }
}
