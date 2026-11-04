using System.Linq;
using System.Numerics;
using Content.Shared.Ghost;
using Content.Shared.Imperial.Cult;
using Content.Shared.Imperial.Cult.Components;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Robust.Client.GameObjects;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Imperial.Cult;

/// <summary>illusion/mock_as(): appearance = original.appearance.</summary>
public sealed class CultIllusionVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CultIllusionComponent, AfterAutoHandleStateEvent>((uid, comp, _) => Copy(uid, comp));
        SubscribeLocalEvent<CultIllusionComponent, ComponentStartup>((uid, comp, _) => Copy(uid, comp));
    }

    private void Copy(EntityUid uid, CultIllusionComponent comp)
    {
        if (comp.Source is not { } source || !TryComp<SpriteComponent>(source, out var src) || !TryComp<SpriteComponent>(uid, out var dst))
            return;
        _sprite.CopySprite((source, src), (uid, dst));
    }
}
