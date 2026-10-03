using Content.Shared.Sprite;

namespace Content.Server.Imperial.Genetics.Mutations;

/// <summary>
/// Меняет размер носителя, пока мутация активна (карликовость, гигантизм).
/// </summary>
[RegisterComponent]
public sealed partial class GeneticScaleComponent : Component
{
    [DataField]
    public float Scale = 1f;

    /// <summary>Дополнительное растяжение по вертикали (акромегалия: выше, но не шире).</summary>
    [DataField]
    public float Height = 1f;
}

public sealed class GeneticScaleSystem : EntitySystem
{
    [Dependency] private readonly SharedScaleVisualsSystem _scale = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GeneticScaleComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<GeneticScaleComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<GeneticScaleComponent> ent, ref ComponentStartup args)
    {
        _scale.SetSpriteScale(ent, _scale.GetSpriteScale(ent) * new System.Numerics.Vector2(ent.Comp.Scale, ent.Comp.Scale * ent.Comp.Height));
    }

    private void OnShutdown(Entity<GeneticScaleComponent> ent, ref ComponentShutdown args)
    {
        if (TerminatingOrDeleted(ent))
            return;

        _scale.SetSpriteScale(ent, _scale.GetSpriteScale(ent) / new System.Numerics.Vector2(ent.Comp.Scale, ent.Comp.Scale * ent.Comp.Height));
    }
}
