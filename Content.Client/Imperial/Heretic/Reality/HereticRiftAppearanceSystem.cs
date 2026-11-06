using Content.Shared.Imperial.Heretic.Core;
using Content.Shared.Imperial.Heretic.Reality;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic.Reality;

public sealed class HereticRiftAppearanceSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;

    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRealityRiftComponent, ComponentStartup>(OnRiftStartup);
        SubscribeLocalEvent<HereticRealityBreachComponent, ComponentStartup>(OnBreachStartup);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<HereticRealityBreachComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var breach, out var sprite))
        {
            if (breach.FadeStartTime is not { } start)
                continue;

            var t = Math.Clamp((float)((now - start) / breach.FadeDuration), 0f, 1f);
            _sprite.SetColor((uid, sprite), Color.White.WithAlpha(t));

            if (t >= 1f)
                breach.FadeStartTime = null;
        }
    }

    private void OnRiftStartup(Entity<HereticRealityRiftComponent> ent, ref ComponentStartup args)
    {
        if (_player.LocalEntity is not { } player || !HasComp<HereticComponent>(player))
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        sprite.PostShader = _proto.Index(UnshadedShader).Instance();
    }

    private void OnBreachStartup(Entity<HereticRealityBreachComponent> ent, ref ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        _sprite.SetColor((ent, sprite), Color.White.WithAlpha(0f));
        ent.Comp.FadeStartTime = _timing.CurTime;
    }
}
