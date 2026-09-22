using Content.Shared.Imperial.Heretic.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client.Imperial.Heretic;

public sealed class HereticRiftAppearanceSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager    _player = default!;
    [Dependency] private readonly IPrototypeManager _proto  = default!;
    [Dependency] private readonly IGameTiming       _timing = default!;

    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private const float BreachFadeDuration = 4f;

    // breach uid → time when fade-in started
    private readonly Dictionary<EntityUid, TimeSpan> _breachFadeStart = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<HereticRealityRiftComponent,   ComponentStartup>(OnRiftStartup);
        SubscribeLocalEvent<HereticRealityBreachComponent, ComponentStartup>(OnBreachStartup);
        SubscribeLocalEvent<HereticRealityBreachComponent, ComponentShutdown>(OnBreachShutdown);
    }

    private void OnRiftStartup(EntityUid uid, HereticRealityRiftComponent _, ComponentStartup args)
    {
        var player = _player.LocalEntity;
        if (player == null || !HasComp<HereticComponent>(player.Value))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.PostShader = _proto.Index(UnshadedShader).Instance();
    }

    private void OnBreachStartup(EntityUid uid, HereticRealityBreachComponent _, ComponentStartup args)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        sprite.Color = new Color(1f, 1f, 1f, 0f);
        _breachFadeStart[uid] = _timing.CurTime;
    }

    private void OnBreachShutdown(EntityUid uid, HereticRealityBreachComponent _, ComponentShutdown args)
    {
        _breachFadeStart.Remove(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_breachFadeStart.Count == 0)
            return;

        var now = _timing.CurTime;
        var done = new List<EntityUid>();

        foreach (var (uid, startTime) in _breachFadeStart)
        {
            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                done.Add(uid);
                continue;
            }

            var t = Math.Clamp((float)(now - startTime).TotalSeconds / BreachFadeDuration, 0f, 1f);
            sprite.Color = new Color(1f, 1f, 1f, t);

            if (t >= 1f)
                done.Add(uid);
        }

        foreach (var uid in done)
            _breachFadeStart.Remove(uid);
    }
}
