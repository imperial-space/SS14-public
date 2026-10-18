using Content.Client.Light.EntitySystems;
using Content.Shared.Imperial.Epsilon;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Epsilon;

/// <summary>
/// Аварийки во время протокола «Эпсилон» не мигают: без маски «двойной конус» и без вращения луча.
/// Когда код снимают, сервер убирает компонент и мигалка возвращается.
/// </summary>
public sealed class EpsilonSteadyLightSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animations = default!;
    [Dependency] private readonly PointLightSystem _pointLight = default!;
    [Dependency] private readonly RotatingLightSystem _rotating = default!;

    private const string RotatingKey = "rotating_light";

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<EpsilonSteadyLightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<EpsilonSteadyLightComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<EpsilonSteadyLightComponent> ent, ref ComponentStartup args)
    {
        if (TryComp<AnimationPlayerComponent>(ent, out var player))
            _animations.Stop(ent, player, RotatingKey);

        if (!TryComp<PointLightComponent>(ent, out var light))
            return;

        ent.Comp.OriginalMask = light.MaskPath;
        _pointLight.SetMask(null, light);
    }

    private void OnShutdown(Entity<EpsilonSteadyLightComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (TryComp<PointLightComponent>(ent, out var light))
            _pointLight.SetMask(ent.Comp.OriginalMask, light);

        _rotating.PlayAnimation(ent);
    }
}
