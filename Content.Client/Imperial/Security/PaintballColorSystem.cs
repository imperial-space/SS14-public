using Content.Shared.Imperial.Security;
using Robust.Client.GameObjects;
using Robust.Shared.GameStates;

namespace Content.Client.Imperial.Security;

/// <summary>Красит шарик и пятно краски в выбранный на маркере цвет.</summary>
public sealed class PaintballColorSystem : EntitySystem
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PaintballColorComponent, ComponentStartup>(OnApply);
        SubscribeLocalEvent<PaintballColorComponent, AfterAutoHandleStateEvent>(OnApply);
    }

    private void OnApply<T>(Entity<PaintballColorComponent> ent, ref T args)
    {
        Apply(ent);
    }

    private void Apply(Entity<PaintballColorComponent> ent)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            _sprite.SetColor((ent, sprite), ent.Comp.Color);
    }
}
