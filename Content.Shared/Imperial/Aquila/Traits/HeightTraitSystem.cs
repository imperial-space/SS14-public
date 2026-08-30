using Robust.Shared.GameObjects;
using Robust.Shared.Serialization;
using Robust.Shared.GameStates;
using Content.Shared.Sprite;
using System.Numerics;

namespace Content.Shared.Imperial.Aquila.Traits;

public sealed class HeightTraitSystem : EntitySystem
{
    [Dependency] private readonly SharedScaleVisualsSystem _scaleVisuals = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<HeightTraitComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<HeightTraitComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(EntityUid uid, HeightTraitComponent component, ComponentStartup args)
    {
        component.BaseScale = _scaleVisuals.GetSpriteScale(uid);
        ApplyModifier(uid, component);
    }

    private void OnShutdown(EntityUid uid, HeightTraitComponent component, ComponentShutdown args)
    {
        if (component.BaseScale is { } baseScale)
            _scaleVisuals.SetSpriteScale(uid, baseScale);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<HeightTraitComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            if (component.Modifier.Equals(component.LastAppliedModifier))
                continue;

            ApplyModifier(uid, component);
        }
    }

    private void ApplyModifier(EntityUid uid, HeightTraitComponent component)
    {
        var baseScale = component.BaseScale ?? Vector2.One;
        var newScale = baseScale * component.Modifier;

        _scaleVisuals.SetSpriteScale(uid, newScale);
        component.LastAppliedModifier = component.Modifier;
    }
}
