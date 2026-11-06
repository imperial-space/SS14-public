using System.Linq;
using Content.Shared.Imperial.Xenobiology.Components;
using Robust.Client.GameObjects;

namespace Content.Client.Imperial.Xenobiology;

/// <summary>Окраска существ эффектами кроссбридинга (последний добавленный цвет).</summary>
public sealed class SlimeTintVisualsSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<SlimeTintComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<SlimeTintComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<SlimeTintComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<SlimeTintComponent> ent, ref ComponentStartup args) => Apply(ent);

    private void OnState(Entity<SlimeTintComponent> ent, ref AfterAutoHandleStateEvent args) => Apply(ent);

    private void OnShutdown(Entity<SlimeTintComponent> ent, ref ComponentShutdown args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent.Owner, sprite), Color.White);
    }

    private void Apply(Entity<SlimeTintComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;
        var color = ent.Comp.Tints.Count == 0 ? Color.White : ent.Comp.Tints.Values.Last();
        _sprite.SetColor((ent.Owner, sprite), color);
    }
}
